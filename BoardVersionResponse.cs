using Redbox.HAL.Component.Model;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class BoardVersionResponse : IBoardVersionResponse
    {
        public bool ReadSuccess { get; private set; }

        public string Version { get; private set; }

        public string BoardName { get; private set; }

        public override string ToString()
        {
            return !this.ReadSuccess ? ErrorCodes.Timeout.ToString().ToUpper() : this.Version;
        }

        internal BoardVersionResponse(ControlBoards board, CoreResponse response)
        {
            this.ReadSuccess = response.Success;
            this.Version = this.ReadSuccess ? response.OpCodeResponse.Trim() : string.Empty;
            this.BoardName = board.ToString();
        }
    }
}
