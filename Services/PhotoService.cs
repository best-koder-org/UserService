using UserService.DTOs;

namespace UserService.Services
{
    public interface IPhotoService
    {
        Task<PhotoResponseDto> UploadPhotoAsync(int userId, PhotoUploadDto photoDto);
        Task<bool> DeletePhotoAsync(int userId, string photoUrl);
        Task<bool> SetPrimaryPhotoAsync(int userId, string photoUrl);
        Task<List<PhotoResponseDto>> GetUserPhotosAsync(int userId);
        Task<bool> ValidatePhotoAsync(IFormFile photo);
    }

    public class PhotoService : IPhotoService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PhotoService> _logger;

        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly long _maxFileSize = 5 * 1024 * 1024; // 5MB

        public PhotoService(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<PhotoService> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<PhotoResponseDto> UploadPhotoAsync(int userId, PhotoUploadDto photoDto)
        {
            try
            {
                if (!await ValidatePhotoAsync(photoDto.Photo))
                {
                    throw new ArgumentException("Invalid photo file");
                }

                var uploadsPath = Path.Combine(_environment.WebRootPath ?? "", "uploads", "photos", userId.ToString());
                Directory.CreateDirectory(uploadsPath);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(photoDto.Photo.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await photoDto.Photo.CopyToAsync(stream);
                }

                var photoUrl = $"/uploads/photos/{userId}/{fileName}";

                return new PhotoResponseDto
                {
                    PhotoUrl = photoUrl,
                    IsPrimary = photoDto.IsPrimary,
                    UploadedAt = DateTime.UtcNow,
                    Description = photoDto.Description
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading photo for user {userId}");
                throw;
            }
        }

        public Task<bool> DeletePhotoAsync(int userId, string photoUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(photoUrl) || !photoUrl.Contains($"/uploads/photos/{userId}/"))
                {
                    return Task.FromResult(false);
                }

                var webRootPath = _environment.WebRootPath ?? "";
                var filePath = Path.Combine(webRootPath, photoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting photo {photoUrl} for user {userId}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> SetPrimaryPhotoAsync(int userId, string photoUrl)
        {
            // This would update the database to set the primary photo
            // Implementation depends on your data access pattern
            return Task.FromResult(true);
        }

        public Task<List<PhotoResponseDto>> GetUserPhotosAsync(int userId)
        {
            try
            {
                var photosPath = Path.Combine(_environment.WebRootPath ?? "", "uploads", "photos", userId.ToString());

                if (!Directory.Exists(photosPath))
                {
                    return Task.FromResult(new List<PhotoResponseDto>());
                }

                var files = Directory.GetFiles(photosPath)
                    .Where(f => _allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .Select(f => new PhotoResponseDto
                    {
                        PhotoUrl = $"/uploads/photos/{userId}/{Path.GetFileName(f)}",
                        UploadedAt = File.GetCreationTime(f),
                        IsPrimary = false // This would be determined from database
                    })
                    .OrderByDescending(p => p.UploadedAt)
                    .ToList();

                return Task.FromResult(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting photos for user {userId}");
                return Task.FromResult(new List<PhotoResponseDto>());
            }
        }

        public async Task<bool> ValidatePhotoAsync(IFormFile photo)
        {
            if (photo == null || photo.Length == 0)
                return false;

            if (photo.Length > _maxFileSize)
                return false;

            var extension = Path.GetExtension(photo.FileName).ToLower();
            if (!_allowedExtensions.Contains(extension))
                return false;

            // Additional validation: check if it's actually an image
            try
            {
                using var stream = photo.OpenReadStream();
                var buffer = new byte[8];
                await stream.ReadAsync(buffer, 0, 8);

                // Check for common image file signatures
                var isImage = IsImageFile(buffer, extension);
                return isImage;
            }
            catch
            {
                return false;
            }
        }

        private bool IsImageFile(byte[] buffer, string extension)
        {
            // JPEG
            if (extension == ".jpg" || extension == ".jpeg")
                return buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xD8;

            // PNG
            if (extension == ".png")
                return buffer.Length >= 8 && buffer[0] == 0x89 && buffer[1] == 0x50 &&
                       buffer[2] == 0x4E && buffer[3] == 0x47;

            // WebP
            if (extension == ".webp")
                return buffer.Length >= 4 && buffer[0] == 0x52 && buffer[1] == 0x49 &&
                       buffer[2] == 0x46 && buffer[3] == 0x46;

            return false;
        }
    }
}
