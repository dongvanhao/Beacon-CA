using Beacon.Domain.Common;
using Beacon.Domain.Entities.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Checkins
{ //Lưu Ảnh/file đính kèm của check-in, có thể bao gồm hình ảnh, video hoặc tài liệu liên quan đến tình trạng an toàn của người dùng tại thời điểm check-in.
    public class CheckinMedia : AuditableEntity
    {
        public Guid CheckinId { get; private set; }
        public Guid MediaObjectId { get; private set; } //Liên kết đến MediaObject để lưu trữ thông tin về file đính kèm, bao gồm đường dẫn, loại file, kích thước, v.v.

        public int SortOrder { get; private set; } //Thuộc tính này có thể được sử dụng để xác định thứ tự hiển thị của các media liên quan đến một check-in, giúp người dùng dễ dàng xem các media theo trình tự thời gian hoặc theo cách mà họ muốn sắp xếp.
        public bool IsPrimary { get; private set; } //Thuộc tính này có thể được sử dụng để đánh dấu một media cụ thể là media chính hoặc nổi bật nhất liên quan đến check-in, giúp người dùng dễ dàng nhận biết và tập trung vào media quan trọng nhất khi xem thông tin check-in.
        public string? Caption { get; private set; } //Thuộc tính này có thể được sử dụng để lưu trữ mô tả hoặc chú thích liên quan đến media, giúp người dùng hiểu rõ hơn về nội dung hoặc ý nghĩa của media khi xem thông tin check-in.

        public Checkin Checkin { get; private set; } = default!;
        public MediaObject MediaObject { get; private set; } = default!;
        protected CheckinMedia() { }

        public static CheckinMedia Create(Guid checkinId, Guid mediaObjectId,
            int sortOrder = 0, bool isPrimary = false, string? caption = null)
            => new()
            {
                CheckinId = checkinId,
                MediaObjectId = mediaObjectId,
                SortOrder = sortOrder,
                IsPrimary = isPrimary,
                Caption = caption
            };
    }
}
