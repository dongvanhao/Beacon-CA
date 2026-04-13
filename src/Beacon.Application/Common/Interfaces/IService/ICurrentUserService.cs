using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Application.Common.Interfaces.IService
{
    public interface ICurrentUserService
    {
        Guid UserId { get; }
        Guid DeviceId { get; }
        string Username { get; }
        bool IsAuthenticated { get; }
    }
}
