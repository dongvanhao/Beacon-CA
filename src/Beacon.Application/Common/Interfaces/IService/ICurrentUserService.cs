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
        string Email { get; }
        bool IsAuthenticated { get; }
    }
}
