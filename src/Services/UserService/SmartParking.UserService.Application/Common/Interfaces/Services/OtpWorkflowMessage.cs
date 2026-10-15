namespace UserService.Application.Common.Interfaces.Services;

public static class OtpWorkflowMessage
{
    public static WorkflowMessage Create(string channel, string destination, string code) =>
        new(channel, destination, "SmartPark verification", $"Your code is {code}. Valid for 5 minutes.", code);
}
