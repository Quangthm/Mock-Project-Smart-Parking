using Microsoft.AspNetCore.DataProtection;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;
namespace SmartParking.UserService.Tests;
internal sealed class TestWorkflowProtector:IWorkflowProtector
{
    private static readonly IDataProtector Protector=new EphemeralDataProtectionProvider().CreateProtector("test-workflow");
    public string Protect(string value)=>Protector.Protect(value);
    public string Unprotect(string value)=>Protector.Unprotect(value);
}
internal static class WorkflowTestSupport
{
    public static AccountWorkflowService Create(AppDbContext db,TimeProvider clock)=>new(db,new BcryptPasswordService(),new TestWorkflowProtector(),clock);
}
