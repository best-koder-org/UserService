using UserService.Models;
using UserService.DTOs;
using UserService.Data;
using Microsoft.EntityFrameworkCore;

namespace UserService.Services
{
    public interface IVerificationService
    {
        Task<bool> RequestPhotoVerificationAsync(int userId, VerificationRequestDto request);
        Task<bool> RequestPhoneVerificationAsync(int userId, string phoneNumber);
        Task<bool> VerifyPhoneCodeAsync(int userId, string code);
        Task<bool> RequestEmailVerificationAsync(int userId);
        Task<bool> VerifyEmailTokenAsync(int userId, string token);
        Task<bool> ProcessPhotoVerificationAsync(int userId, bool isApproved);
        Task<Dictionary<string, bool>> GetVerificationStatusAsync(int userId);
    }

    public class VerificationService : IVerificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly ILogger<VerificationService> _logger;
        private readonly IConfiguration _configuration;

        // In-memory storage for demo purposes. In production, use Redis or database
        private static readonly Dictionary<int, string> _phoneVerificationCodes = new();
        private static readonly Dictionary<int, string> _emailVerificationTokens = new();

        public VerificationService(
            ApplicationDbContext context,
            IPhotoService photoService,
            ILogger<VerificationService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _photoService = photoService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> RequestPhotoVerificationAsync(int userId, VerificationRequestDto request)
        {
            try
            {
                var user = await _context.UserProfiles.FindAsync(userId);
                if (user == null)
                    return false;

                if (!await _photoService.ValidatePhotoAsync(request.VerificationPhoto))
                    return false;

                // Upload verification photo
                var photoDto = new PhotoUploadDto
                {
                    Photo = request.VerificationPhoto,
                    Description = "Verification photo"
                };

                var photoResponse = await _photoService.UploadPhotoAsync(userId, photoDto);

                // Store verification photo URL
                user.VerificationPhotoUrl = photoResponse.PhotoUrl;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // In a real app, this would trigger a manual review process
                // For demo, we'll auto-approve after a delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(5)); // Simulate review time
                    await ProcessPhotoVerificationAsync(userId, true);
                });

                _logger.LogInformation($"Photo verification requested for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting photo verification for user {userId}");
                return false;
            }
        }

        public async Task<bool> RequestPhoneVerificationAsync(int userId, string phoneNumber)
        {
            try
            {
                var user = await _context.UserProfiles.FindAsync(userId);
                if (user == null)
                    return false;

                // Generate 6-digit code
                var code = new Random().Next(100000, 999999).ToString();
                _phoneVerificationCodes[userId] = code;

                // In a real app, send SMS using Twilio, AWS SNS, etc.
                _logger.LogInformation($"SMS verification code for user {userId}: {code}");

                // Remove code after 10 minutes
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(10));
                    _phoneVerificationCodes.Remove(userId);
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting phone verification for user {userId}");
                return false;
            }
        }

        public async Task<bool> VerifyPhoneCodeAsync(int userId, string code)
        {
            try
            {
                if (!_phoneVerificationCodes.TryGetValue(userId, out var storedCode) || storedCode != code)
                    return false;

                var user = await _context.UserProfiles.FindAsync(userId);
                if (user == null)
                    return false;

                user.IsPhoneVerified = true;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _phoneVerificationCodes.Remove(userId);
                _logger.LogInformation($"Phone verified successfully for user {userId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying phone code for user {userId}");
                return false;
            }
        }

        public async Task<bool> RequestEmailVerificationAsync(int userId)
        {
            try
            {
                var user = await _context.UserProfiles.FindAsync(userId);
                if (user == null)
                    return false;

                var token = Guid.NewGuid().ToString("N");
                _emailVerificationTokens[userId] = token;

                // In a real app, send email using SendGrid, AWS SES, etc.
                var verificationLink = $"https://yourapp.com/verify-email?userId={userId}&token={token}";
                _logger.LogInformation($"Email verification link for user {userId}: {verificationLink}");

                // Remove token after 24 hours
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(24));
                    _emailVerificationTokens.Remove(userId);
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting email verification for user {userId}");
                return false;
            }
        }

        public async Task<bool> VerifyEmailTokenAsync(int userId, string token)
        {
            try
            {
                if (!_emailVerificationTokens.TryGetValue(userId, out var storedToken) || storedToken != token)
                    return false;

                var user = await _context.UserProfiles.FindAsync(userId);
                if (user == null)
                    return false;

                user.IsEmailVerified = true;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _emailVerificationTokens.Remove(userId);
                _logger.LogInformation($"Email verified successfully for user {userId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying email token for user {userId}");
                return false;
            }
        }

        public async Task<bool> ProcessPhotoVerificationAsync(int userId, bool isApproved)
        {
            try
            {
                var user = await _context.UserProfiles.FindAsync(userId);
                if (user == null)
                    return false;

                user.IsPhotoVerified = isApproved;
                if (isApproved)
                {
                    user.IsVerified = user.IsEmailVerified && user.IsPhoneVerified && user.IsPhotoVerified;
                    user.VerificationDate = DateTime.UtcNow;
                }
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Photo verification {(isApproved ? "approved" : "rejected")} for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing photo verification for user {userId}");
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> GetVerificationStatusAsync(int userId)
        {
            try
            {
                var user = await _context.UserProfiles.FindAsync(userId);
                if (user == null)
                    return new Dictionary<string, bool>();

                return new Dictionary<string, bool>
                {
                    { "email", user.IsEmailVerified },
                    { "phone", user.IsPhoneVerified },
                    { "photo", user.IsPhotoVerified },
                    { "overall", user.IsVerified }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting verification status for user {userId}");
                return new Dictionary<string, bool>();
            }
        }
    }
}
