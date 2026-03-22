using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Shared.Results
{
    #region Result<T> là đối tượng biểu diễn kết quả của một hoạt động, có thể thành công hoặc thất bại, kèm theo dữ liệu trả về nếu thành công và thông tin lỗi nếu thất bại.
    /* Result<T> dùng khi thành công có data trả về.
        Ví dụ:
        login trả token
        get profile trả user data
        get safety status trả trạng thái hôm nay
     */
    #endregion
    public class Result<T> : Result
    {
        private readonly T? _value;

        protected Result(T? value, bool isSuccess, Error error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        public T Value => IsSuccess 
            ? _value! 
            : throw new InvalidOperationException("Cannot access the value of a failed result.");
        public static Result<T> Success(T value) => new(value, true, Error.None);
        public static Result<T> Failure(Error error) => new(default, false, error);
    }
}
