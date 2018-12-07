using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public enum ErrorCode
    {
        None = 0,
        InternalError,
        CannotDestroyAccountWithSocialLink
    }

    [Serializable]
    public class ErrorCodeException : Exception, ISerializable
    {
        public ErrorCode ErrorCode { get; private set; }


        public ErrorCodeException(ErrorCode ErrorCode) : base($"ErrorCode {ErrorCode}") { }

        public ErrorCodeException(ErrorCode ErrorCode, Exception innerException) : base($"ErrorCode {ErrorCode}", innerException) { }

        protected ErrorCodeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            ErrorCode = (ErrorCode)info.GetInt32("ec");
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("ec", (int)ErrorCode);
        }
    }
}
