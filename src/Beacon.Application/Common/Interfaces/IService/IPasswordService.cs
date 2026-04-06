namespace Beacon.Application.Common.Interfaces.IService
{
    public interface IPasswordService
    {
        bool Verify(string rawPassword, string passwordHash);
    }
}
