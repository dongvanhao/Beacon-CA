using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums.Checkins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Checkins
{
    public class Checkin : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public Guid DailySafetyRecordId { get; private set; }

        public DateOnly CheckinDate { get; private set; }
        public DateTime CheckedInAtUtc { get; private set; }

        public CheckinType Type { get; private set; } = CheckinType.Manual;
        public string? Note { get; private set; }

        public decimal? Latitude { get; private set; }
        public decimal? Longitude { get; private set; }

        public User User { get; private set; } = default!;
        public DailySafetyRecord DailySafetyRecord { get; private set; } = default!;
        public ICollection<CheckinMedia> MediaItems { get; private set; } = new List<CheckinMedia>();

        protected Checkin() { }

        public static Checkin Create(Guid userId, Guid dailySafetyRecordId, CheckinType type,
            DateOnly checkinDate, string? note = null, decimal? latitude = null, decimal? longitude = null)
        {
            return new()
            {
                UserId = userId,
                DailySafetyRecordId = dailySafetyRecordId,
                CheckinDate = checkinDate,
                CheckedInAtUtc = DateTime.UtcNow,
                Type = type,
                Note = note,
                Latitude = latitude,
                Longitude = longitude
            };
        }
    }
}
