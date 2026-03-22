using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Shared.Results
{
    #region Results là đối tượng biểu diễn kết quả của một hoạt động, có thể thành công hoặc thất bại, kèm theo thông tin lỗi nếu có.
    /*
     * Result biểu diễn:
        thành công
        thất bại
        Nhưng không có data trả về.
        Ví dụ:
        xoá thành công
        cập nhật thành công
        xác nhận alert thành công
     */
    #endregion
    public class Result
    {
        protected Result(bool isSuccess, Error error)
        {
            if(isSuccess && error != Error.None)
                throw new ArgumentException("A successful result cannot have an error.");

            if (!isSuccess && error == Error.None)
                throw new ArgumentException("A failed result must have an error.");

            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }

        public static Result Success() => new(true, Error.None);
        public static Result Failure(Error error) => new(false, error);

    }
}
