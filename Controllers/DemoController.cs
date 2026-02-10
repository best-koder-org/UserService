using Microsoft.AspNetCore.Mvc;
using UserService.DTOs;
using System.Text.Json;

namespace UserService.Controllers
{
    [Route("api/demo")]
    [ApiController]
    public class DemoController : ControllerBase
    {
        private readonly ILogger<DemoController> _logger;

        public DemoController(ILogger<DemoController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Returns demo user profiles for testing and development
        /// </summary>
        [HttpGet("profiles")]
        public ActionResult<List<UserProfileSummaryDto>> GetDemoProfiles([FromQuery] int count = 10)
        {
            try
            {
                var demoProfiles = GenerateDemoProfiles(count);
                _logger.LogInformation($"Generated {demoProfiles.Count} demo profiles");
                return Ok(demoProfiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating demo profiles");
                return StatusCode(500, "Error generating demo profiles");
            }
        }

        /// <summary>
        /// Returns a specific demo user profile by ID
        /// </summary>
        [HttpGet("profiles/{id:int}")]
        public ActionResult<UserProfileDetailDto> GetDemoProfile(int id)
        {
            try
            {
                var profile = GenerateDetailedDemoProfile(id);
                _logger.LogInformation($"Generated detailed demo profile for ID {id}");
                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating demo profile for ID {id}");
                return StatusCode(500, "Error generating demo profile");
            }
        }

        /// <summary>
        /// Returns demo search results
        /// </summary>
        [HttpPost("search")]
        public ActionResult<SearchResultDto<UserProfileSummaryDto>> SearchDemoProfiles([FromBody] SearchUsersDto searchDto)
        {
            try
            {
                var allProfiles = GenerateDemoProfiles(50); // Generate larger pool for search
                
                // Apply simple filtering for demo
                var filteredProfiles = allProfiles.AsEnumerable();

                if (searchDto.MinAge.HasValue)
                    filteredProfiles = filteredProfiles.Where(p => p.Age >= searchDto.MinAge.Value);
                
                if (searchDto.MaxAge.HasValue)
                    filteredProfiles = filteredProfiles.Where(p => p.Age <= searchDto.MaxAge.Value);

                var results = filteredProfiles
                    .Skip((searchDto.Page - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToList();

                var totalCount = filteredProfiles.Count();
                var totalPages = (int)Math.Ceiling((double)totalCount / searchDto.PageSize);

                var searchResult = new SearchResultDto<UserProfileSummaryDto>
                {
                    Results = results,
                    TotalCount = totalCount,
                    Page = searchDto.Page,
                    PageSize = searchDto.PageSize,
                    TotalPages = totalPages,
                    HasNext = searchDto.Page < totalPages,
                    HasPrevious = searchDto.Page > 1
                };

                _logger.LogInformation($"Demo search returned {results.Count} profiles");
                return Ok(searchResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing demo search");
                return StatusCode(500, "Error performing demo search");
            }
        }

        /// <summary>
        /// Health check for demo endpoints
        /// </summary>
        [HttpGet("health")]
        public IActionResult DemoHealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Service = "UserService Demo Mode",
                AvailableEndpoints = new[]
                {
                    "GET /api/demo/profiles",
                    "GET /api/demo/profiles/{id}",
                    "POST /api/demo/search"
                }
            });
        }

        #region Private Helper Methods

        private List<UserProfileSummaryDto> GenerateDemoProfiles(int count)
        {
            var profiles = new List<UserProfileSummaryDto>();
            var names = new[]
            {
                "Emma Johnson", "Sofia Martinez", "Isabella Thompson", "Olivia Garcia", "Ava Rodriguez",
                "Mia Williams", "Amelia Brown", "Charlotte Davis", "Luna Miller", "Harper Wilson",
                "Evelyn Moore", "Abigail Taylor", "Emily Anderson", "Elizabeth Thomas", "Sofia Jackson",
                "Avery White", "Ella Harris", "Scarlett Martin", "Grace Lee", "Aria Clark"
            };

            var cities = new[]
            {
                "Stockholm", "Gothenburg", "Malmö", "Uppsala", "Västerås",
                "Örebro", "Linköping", "Helsingborg", "Jönköping", "Norrköping"
            };

            var occupations = new[]
            {
                "Software Engineer", "Designer", "Teacher", "Nurse", "Marketing Manager",
                "Data Scientist", "Photographer", "Architect", "Consultant", "Student"
            };

            var bios = new[]
            {
                "Love hiking and photography 📸",
                "Yoga instructor & coffee enthusiast ☕",
                "Chef who loves to cook for friends 👩‍🍳",
                "Adventure seeker and book lover 📚",
                "Dog mom and travel addict ✈️",
                "Artist and music lover 🎨",
                "Fitness enthusiast and foodie 🏃‍♀️",
                "Nature lover and weekend explorer 🌲",
                "Dancer and life enjoyer 💃",
                "Entrepreneur with wanderlust 🌍"
            };

            var interests = new[]
            {
                new[] { "Photography", "Hiking", "Travel" },
                new[] { "Yoga", "Coffee", "Art" },
                new[] { "Cooking", "Wine", "Music" },
                new[] { "Reading", "Movies", "Adventure" },
                new[] { "Dogs", "Travel", "Beaches" },
                new[] { "Art", "Music", "Concerts" },
                new[] { "Fitness", "Food", "Running" },
                new[] { "Nature", "Camping", "Outdoors" },
                new[] { "Dancing", "Parties", "Fun" },
                new[] { "Business", "Travel", "Innovation" }
            };

            var promptQuestions = new[]
            {
                "A life goal of mine",
                "My simple pleasures",
                "I get along best with people who",
                "Together, we could",
                "The way to win me over is",
                "My most controversial opinion",
                "I'm looking for",
                "My go-to karaoke song",
                "Two truths and a lie",
                "The key to my heart is",
            };

            var promptAnswers = new[]
            {
                "Travel to every continent before 40",
                "Sunday mornings with coffee and a good book",
                "Are curious about the world and love trying new things",
                "Explore hidden food spots and take spontaneous road trips",
                "Good conversation and a sense of humor",
                "Pineapple absolutely belongs on pizza",
                "Someone who makes ordinary moments feel extraordinary",
                "Don't Stop Believin' by Journey — no shame",
                "I've been skydiving, I can cook a 5-course meal, I once met the King",
                "Genuine kindness and a love for adventure",
                "Learn to surf on a tropical island together",
                "A cozy night in with homemade pasta and a movie",
                "Can laugh at themselves and appreciate the little things",
                "Go on a camping trip under the northern lights",
                "Eye contact, thoughtful questions, and remembering the little things",
                "Cats are better than dogs — fight me",
                "A partner in crime who's also my best friend",
                "Bohemian Rhapsody — full dramatic performance included",
                "I speak 4 languages, I've lived in 6 countries, I can juggle",
                "Home-cooked meals and honest conversations",
            };

            var educations = new[]
            {
                "Stockholm University", "Lund University", "KTH Royal Institute",
                "Gothenburg University", "Uppsala University", "Chalmers",
                "Malmö University", "Linköping University", "Umeå University",
                "Karolinska Institute"
            };

            var genders = new[] { "Woman", "Woman", "Woman", "Woman", "Woman", "Woman", "Man", "Non-binary", "Woman", "Woman" };
            var heights = new[] { 165, 170, 158, 175, 162, 168, 182, 173, 160, 171 };

            for (int i = 0; i < count; i++)
            {
                var nameIndex = i % names.Length;
                var interestIndex = i % interests.Length;

                // Generate 3-6 photo URLs per profile
                var photoCount = 3 + (i % 4); // 3-6 photos
                var photoUrls = new List<string>();
                for (int p = 0; p < photoCount; p++)
                {
                    photoUrls.Add($"https://picsum.photos/400/600?random={i * 10 + p + 1}");
                }

                // Generate 2-3 prompts per profile
                var prompts = new List<PromptAnswer>();
                var promptCount = 2 + (i % 2); // 2 or 3 prompts
                for (int q = 0; q < promptCount; q++)
                {
                    var qIdx = (i * 3 + q) % promptQuestions.Length;
                    var aIdx = (i * 3 + q) % promptAnswers.Length;
                    prompts.Add(new PromptAnswer
                    {
                        Question = promptQuestions[qIdx],
                        Answer = promptAnswers[aIdx]
                    });
                }

                // Voice prompt for every 3rd profile
                string? voicePromptUrl = (i % 3 == 0)
                    ? $"https://example.com/voice-prompts/profile-{i + 1}.m4a"
                    : null;

                profiles.Add(new UserProfileSummaryDto
                {
                    Id = i + 1,
                    Name = names[nameIndex],
                    Age = 22 + (i % 15),
                    City = cities[i % cities.Length],
                    PrimaryPhotoUrl = photoUrls[0],
                    PhotoUrls = photoUrls,
                    Bio = bios[i % bios.Length],
                    Occupation = occupations[i % occupations.Length],
                    Education = educations[i % educations.Length],
                    Height = heights[i % heights.Length],
                    Gender = genders[i % genders.Length],
                    Interests = interests[interestIndex].ToList(),
                    Prompts = prompts,
                    VoicePromptUrl = voicePromptUrl,
                    IsVerified = i % 3 == 0,
                    IsOnline = i % 4 != 0,
                    LastActiveAt = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(0, 1440))
                });
            }

            return profiles;
        }

        private UserProfileDetailDto GenerateDetailedDemoProfile(int id)
        {
            var summaryProfile = GenerateDemoProfiles(1).First();
            summaryProfile.Id = id;

            return new UserProfileDetailDto
            {
                Id = id,
                Name = summaryProfile.Name,
                Email = $"demo.user.{id}@example.com",
                Bio = summaryProfile.Bio,
                Age = summaryProfile.Age,
                Gender = id % 2 == 0 ? "Female" : "Male",
                Preferences = "Everyone",
                SexualOrientation = "Straight",
                City = summaryProfile.City,
                State = "Stockholm County",
                Country = "Sweden",
                PhotoUrls = new List<string>
                {
                    $"https://picsum.photos/400/600?random={id}",
                    $"https://picsum.photos/400/600?random={id + 100}",
                    $"https://picsum.photos/400/600?random={id + 200}"
                },
                PrimaryPhotoUrl = summaryProfile.PrimaryPhotoUrl,
                Occupation = summaryProfile.Occupation,
                Company = $"Demo Company {id}",
                Education = "University Graduate",
                School = "Stockholm University",
                Height = 160 + (id % 30), // Heights 160-189cm
                Religion = "Not specified",
                Ethnicity = "Not specified",
                SmokingStatus = "Non-smoker",
                DrinkingStatus = "Social drinker",
                WantsChildren = id % 3 == 0,
                HasChildren = false,
                RelationshipType = "Long-term",
                Interests = summaryProfile.Interests,
                Languages = new List<string> { "Swedish", "English" },
                HobbyList = string.Join(", ", summaryProfile.Interests),
                InstagramHandle = $"@{summaryProfile.Name.ToLower().Replace(" ", "")}",
                SpotifyTopArtists = "Spotify not connected",
                IsVerified = summaryProfile.IsVerified,
                IsPhoneVerified = true,
                IsEmailVerified = true,
                IsPhotoVerified = summaryProfile.IsVerified,
                IsPremium = id % 5 == 0, // Every 5th profile is premium
                SubscriptionType = id % 5 == 0 ? "Premium" : "Free",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 365)),
                LastActiveAt = summaryProfile.LastActiveAt,
                IsOnline = summaryProfile.IsOnline
            };
        }

        #endregion
    }
}
