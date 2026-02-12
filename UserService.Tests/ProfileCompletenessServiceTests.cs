using System;
using System.Linq;
using System.Text.Json;
using Xunit;
using UserService.Models;
using UserService.Services;

namespace UserService.Tests
{
    public class ProfileCompletenessServiceTests
    {
        private readonly ProfileCompletenessService _service = new();

        private static UserProfile EmptyProfile() => new()
        {
            Name = "",
            DateOfBirth = default,
            Gender = "",
            PhotoCount = 0,
            Bio = "",
            Interests = "[]",
            Height = 0,
            RelationshipType = "",
            Occupation = "",
            Education = "",
            SmokingStatus = "",
            DrinkingStatus = "",
            Religion = "",
            Languages = "[]",
            Company = "",
            InstagramHandle = "",
            IsVerified = false
        };

        private static UserProfile FullProfile() => new()
        {
            Name = "Alice",
            DateOfBirth = new DateTime(1995, 6, 15),
            Gender = "Female",
            PhotoCount = 6,
            Bio = new string('x', 200), // >150 chars covers both Bio and BioLong
            Interests = JsonSerializer.Serialize(new[] { "hiking", "yoga", "cooking", "travel", "music" }),
            Height = 170,
            RelationshipType = "Serious",
            Occupation = "Engineer",
            Education = "BSc Computer Science",
            SmokingStatus = "Never",
            DrinkingStatus = "Sometimes",
            Religion = "None",
            Languages = JsonSerializer.Serialize(new[] { "English", "Swedish" }),
            Company = "TechCorp",
            InstagramHandle = "@alice",
            IsVerified = true
        };

        [Fact]
        public void EmptyProfile_ReturnsZeroPercent()
        {
            var result = _service.Calculate(EmptyProfile());
            Assert.Equal(0, result.Percentage);
        }

        [Fact]
        public void FullProfile_Returns100Percent()
        {
            var result = _service.Calculate(FullProfile());
            Assert.Equal(100, result.Percentage);
        }

        [Fact]
        public void FullProfile_HasNoMissingFields()
        {
            var result = _service.Calculate(FullProfile());
            Assert.Empty(result.MissingFields);
        }

        [Fact]
        public void FullProfile_NextSuggestion_IsComplete()
        {
            var result = _service.Calculate(FullProfile());
            Assert.Equal("Your profile is complete!", result.NextSuggestion);
        }

        [Fact]
        public void EmptyProfile_MissingFields_ContainsAllFields()
        {
            var result = _service.Calculate(EmptyProfile());
            // 4 required + 6 encouraged + 10 optional = 20 total fields
            Assert.Equal(20, result.MissingFields.Count);
            Assert.Empty(result.FilledFields);
        }

        [Fact]
        public void EmptyProfile_NextSuggestion_IsRequiredField()
        {
            var result = _service.Calculate(EmptyProfile());
            // Should suggest a required field first (highest impact)
            var missingRequired = result.MissingFields.Where(f => f.Tier == "Required");
            Assert.NotEmpty(missingRequired);
            // The nudge text should be one of the required field nudges
            Assert.Contains("Add your name", result.NextSuggestion);
        }

        [Fact]
        public void RequiredFieldsOnly_Returns40Percent()
        {
            var profile = EmptyProfile();
            profile.Name = "Bob";
            profile.DateOfBirth = new DateTime(1990, 1, 1);
            profile.Gender = "Male";
            profile.PhotoCount = 2;

            var result = _service.Calculate(profile);
            Assert.Equal(40, result.Percentage);
        }

        [Fact]
        public void RequiredPlusEncouraged_Returns75Percent()
        {
            var profile = EmptyProfile();
            // Required fields
            profile.Name = "Bob";
            profile.DateOfBirth = new DateTime(1990, 1, 1);
            profile.Gender = "Male";
            profile.PhotoCount = 4; // also covers Photos4Plus encouraged

            // Encouraged fields
            profile.Bio = new string('x', 200); // covers Bio and BioLong
            profile.Interests = JsonSerializer.Serialize(new[] { "a", "b", "c", "d", "e" }); // 5+ interests
            profile.Height = 180;
            profile.RelationshipType = "Serious";

            var result = _service.Calculate(profile);
            Assert.Equal(75, result.Percentage);
        }

        [Fact]
        public void PartialRequired_ReturnsCorrectWeight()
        {
            var profile = EmptyProfile();
            profile.Name = "Charlie"; // 10 of 40 required weight

            var result = _service.Calculate(profile);
            // Required: 10/40 * 100 = 25, weighted 25 * 0.40 = 10
            // Encouraged: 0, Optional: 0
            Assert.Equal(10, result.Percentage);
        }

        [Fact]
        public void BioBetween50And150_FilsBioButNotBioLong()
        {
            var profile = EmptyProfile();
            profile.Bio = new string('x', 75); // 50+ but <150

            var result = _service.Calculate(profile);
            var filledNames = result.FilledFields.Select(f => f.FieldName).ToList();
            Assert.Contains("Bio", filledNames);
            Assert.DoesNotContain("BioLong", filledNames);
        }

        [Fact]
        public void PhotoCount2_FillsMinPhotos_NotPhotos4Plus()
        {
            var profile = EmptyProfile();
            profile.PhotoCount = 2;

            var result = _service.Calculate(profile);
            var filledNames = result.FilledFields.Select(f => f.FieldName).ToList();
            Assert.Contains("MinPhotos", filledNames);
            Assert.DoesNotContain("Photos4Plus", filledNames);
            Assert.DoesNotContain("Photos6Max", filledNames);
        }

        [Fact]
        public void PhotoCount6_FillsAllPhotoFields()
        {
            var profile = EmptyProfile();
            profile.PhotoCount = 6;

            var result = _service.Calculate(profile);
            var filledNames = result.FilledFields.Select(f => f.FieldName).ToList();
            Assert.Contains("MinPhotos", filledNames);
            Assert.Contains("Photos4Plus", filledNames);
            Assert.Contains("Photos6Max", filledNames);
        }

        [Fact]
        public void Interests_LessThan5_NotFilled()
        {
            var profile = EmptyProfile();
            profile.Interests = JsonSerializer.Serialize(new[] { "hiking", "yoga" });

            var result = _service.Calculate(profile);
            var filledNames = result.FilledFields.Select(f => f.FieldName).ToList();
            Assert.DoesNotContain("Interests5", filledNames);
        }

        [Fact]
        public void InvalidJsonInterests_TreatedAsZero()
        {
            var profile = EmptyProfile();
            profile.Interests = "not-json";

            var result = _service.Calculate(profile);
            var filledNames = result.FilledFields.Select(f => f.FieldName).ToList();
            Assert.DoesNotContain("Interests5", filledNames);
        }

        [Fact]
        public void NullFields_DoNotThrow()
        {
            var profile = new UserProfile(); // all defaults
            var result = _service.Calculate(profile);
            Assert.True(result.Percentage >= 0);
            Assert.True(result.Percentage <= 100);
        }

        [Fact]
        public void Percentage_IsClamped_Between0And100()
        {
            var result1 = _service.Calculate(EmptyProfile());
            Assert.InRange(result1.Percentage, 0, 100);

            var result2 = _service.Calculate(FullProfile());
            Assert.InRange(result2.Percentage, 0, 100);
        }

        [Fact]
        public void FilledAndMissing_AreMutuallyExclusive()
        {
            var profile = FullProfile();
            profile.Occupation = ""; // Make one optional field missing

            var result = _service.Calculate(profile);

            var filledNames = result.FilledFields.Select(f => f.FieldName).ToHashSet();
            var missingNames = result.MissingFields.Select(f => f.FieldName).ToHashSet();

            // No overlap
            Assert.Empty(filledNames.Intersect(missingNames));

            // Together cover all fields
            Assert.Equal(20, filledNames.Count + missingNames.Count);
        }

        [Fact]
        public void FieldStatus_HasCorrectTier()
        {
            var result = _service.Calculate(EmptyProfile());

            var requiredFields = result.MissingFields.Where(f => f.Tier == "Required").Select(f => f.FieldName);
            Assert.Contains("Name", requiredFields);
            Assert.Contains("Birthday", requiredFields);
            Assert.Contains("Gender", requiredFields);
            Assert.Contains("MinPhotos", requiredFields);

            var encouragedFields = result.MissingFields.Where(f => f.Tier == "Encouraged").Select(f => f.FieldName);
            Assert.Contains("Bio", encouragedFields);
            Assert.Contains("Height", encouragedFields);

            var optionalFields = result.MissingFields.Where(f => f.Tier == "Optional").Select(f => f.FieldName);
            Assert.Contains("Occupation", optionalFields);
            Assert.Contains("Verified", optionalFields);
        }

        [Fact]
        public void NextSuggestion_PrioritizesRequired_OverEncouraged()
        {
            var profile = EmptyProfile();
            // Fill some optional/encouraged but leave required empty
            profile.Occupation = "Engineer";
            profile.Education = "PhD";

            var result = _service.Calculate(profile);
            // The first missing required field should be Name (weight 10)
            Assert.Contains("Add your name", result.NextSuggestion);
        }

        [Fact]
        public void VerifiedField_IsFilled_WhenTrue()
        {
            var profile = EmptyProfile();
            profile.IsVerified = true;

            var result = _service.Calculate(profile);
            var filledNames = result.FilledFields.Select(f => f.FieldName);
            Assert.Contains("Verified", filledNames);
        }
    }
}
