﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;

namespace Terratype.Providers
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BingMapsV8 : Models.Provider
    {
        [JsonProperty]
        public override string Id
        {
            get
            {
                return "Terratype.BingMapsV8";
            }
        }
        public override string Name
        {
            get
            {
                return "Bing Maps V8";
            }
        }

        public override string Description
        {
            get
            {
                return "terratypeBingMapsV8_description";       //  Value is in language file
            }
        }

        public override string ReferenceUrl
        {
            get
            {
                return "https://www.microsoft.com/maps/choose-your-bing-maps-API.aspx";
            }
        }

        public override IDictionary<string, Type> CoordinateSystems
        {
            get
            {
                var wgs84 = new CoordinateSystems.Wgs84();

                return new Dictionary<string, Type>
                {
                    { wgs84.Id, wgs84.GetType() }
                };
            }
        }

        public override void GetHtml(HtmlTextWriter writer, int mapId, Models.Model model, int height, string language, string labelId)
        {
            throw new NotImplementedException();
        }
    }
}
