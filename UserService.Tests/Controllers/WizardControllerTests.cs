using Xunit;
using Moq;
using MediatR;
using UserService.Controllers;
using Microsoft.Extensions.Logging;

namespace UserService.Tests.Controllers;

public class WizardControllerTests
{
    [Fact(Skip = "T003")]
    public async Task UpdateStepBasicInfo_ValidData_ReturnsOk() { }
    
    [Fact(Skip = "T003")]
    public async Task UpdateStepPreferences_ValidData_ReturnsOk() { }
    
    [Fact(Skip = "T003")]
    public async Task CompleteWizard_WithPhotos_MarksProfileReady() { }
}
