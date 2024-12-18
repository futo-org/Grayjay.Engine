using Grayjay.Engine.Exceptions;
using Grayjay.Engine.Models.General;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Feed
{
    public class PlatformContentPlaceholder : PlatformContent
    {
        public override ContentType ContentType => ContentType.PLACEHOLDER;

        public string PlaceholderIcon { get; set; }

        private Exception _exception;
        public string Error { get; set; }
        public string ErrorPluginID { get; set; }

        public PlatformContentPlaceholder(string pluginId, Exception ex = null, string platformName = null) : base()
        {
            ID = new General.PlatformID()
            {
                PluginID = pluginId,
                Platform = platformName
            };
            _exception = ex;
            Error = _exception?.Message;
            if (ex is PluginException pex)
            {
                ErrorPluginID = pex.Config.ID;
            }
        }
        public PlatformContentPlaceholder(PluginConfig config, Exception ex = null) : base()
        {
            ID = new General.PlatformID()
            {
                PluginID = config.ID,
                Platform = config.Name
            };
            PlaceholderIcon = config.AbsoluteIconUrl;
            _exception = ex;
            Error = _exception?.Message;
            if(ex is PluginException pex)
            {
                ErrorPluginID = pex.Config.ID;
            }
        }
    }
}
