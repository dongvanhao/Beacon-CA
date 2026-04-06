using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Attributes
{
    public class AuthenticatedAttribute : AuthorizeAttribute
    {
        public AuthenticatedAttribute()
        {
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme;
        }
    }
}
