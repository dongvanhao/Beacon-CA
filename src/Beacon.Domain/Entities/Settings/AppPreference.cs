using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Setting
{
    public class AppPreference : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public string LanguageCode { get; private set; } = "vi"; // Mã ngôn ngữ, ví dụ: "en" cho tiếng Anh, "vi" cho tiếng Việt
        public string Theme { get; private set; } = "system"; // Giao diện, có thể là "light", "dark" hoặc "system" để theo hệ thống
        public bool IsOnboardingCompleted { get; private set; } = false; // Trạng thái hoàn thành onboarding

        public User User { get; private set; } = default!;
        protected AppPreference() { }
    }
}
