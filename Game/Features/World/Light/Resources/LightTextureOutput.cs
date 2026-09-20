using Godot;
using System;

namespace Jogo25D.Light
{
    public sealed class LightTextureOutput : IDisposable
    {
        #region Constructors

        public LightTextureOutput(Rid rid)
        {
            _rid = rid;
            Texture = new Texture2Drd
            {
                TextureRdRid = rid
            };
        }

        public LightTextureOutput(Texture2D texture)
        {
            Texture = texture;
        }

        #endregion

        #region Dinamic properties

        public Texture2D Texture { get; }

        private Rid _rid;

        #endregion

        #region Core - Descarte

        public void Dispose()
        {
            if (_rid.IsValid)
            {
                var rid = _rid;

                _rid = default;

                if (Texture is Texture2Drd texture)
                {
                    texture.TextureRdRid = default;
                }

                RenderingServer.CallOnRenderThread(Callable.From(() => RenderingServer.GetRenderingDevice()?.FreeRid(rid)));
            }

            Texture.Dispose();
        }

        #endregion
    }
}
