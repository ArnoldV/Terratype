﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Terratype.Controllers
{
    [Umbraco.Web.Mvc.PluginController("terratype")]
    public class AjaxController : Umbraco.Web.Editors.UmbracoAuthorizedJsonController
    {
        public class CoordinateSystemsJson
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }

            public string referenceUrl { get; set; }

            public int precision { get; set; }

            public CoordinateSystemsJson(Models.Position position)
            {
                id = position.Id;
                name = position.Name;
                description = position.Description;
                referenceUrl = position.ReferenceUrl;
                precision = position.Precision;
            }
        }


        public class ProviderJson
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string referenceUrl { get; set; }
            public IEnumerable<CoordinateSystemsJson> coordinateSystems { get; set; }
            public bool canSearch { get; set; }

            public ProviderJson(Models.Provider provider)
            {
                id = provider.Id;
                name = provider.Name;
                description = provider.Description;
                referenceUrl = provider.ReferenceUrl;
                coordinateSystems = provider.CoordinateSystems.Select(x => new CoordinateSystemsJson(Models.Position.Create(x.Key)));
                canSearch = provider.CanSearch;
            }
        }

        [System.Web.Http.HttpGet]
        public IEnumerable<ProviderJson> Providers()
        {
            var providers = new List<ProviderJson>();
            foreach (var item in Models.Provider.Providers)
            {
                providers.Add(new ProviderJson(Models.Provider.Create(item.Value)));
            }
            return providers;
        }

        [System.Web.Http.HttpGet]
        public string Parse(string id, string datum)
        {
            var position = Models.Position.Create(id);
            if (position == null || !position.TryParse(datum))
            {
                return null;
            }
            return position._datum.ToString();
        }

        public class ImageInfo
        {
            public string format { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public HttpStatusCode status { get; set; }
            public string error { get; set; }
        }

        private short ReadLittleEndianInt16(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(Int16)];
            for (int i = 0; i != sizeof(Int16); i++)
            {
                bytes[sizeof(short) - 1 - i] = binaryReader.ReadByte();
            }
            return BitConverter.ToInt16(bytes, 0);
        }

        private int ReadLittleEndianInt32(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(Int32)];
            for (int i = 0; i != sizeof(Int32); i++)
            {
                bytes[sizeof(int) - 1 - i] = binaryReader.ReadByte();
            }
            return BitConverter.ToInt32(bytes, 0);
        }

        private bool Match(byte[] thisBytes, byte[] thatBytes)
        {
            for (int i = 0; i != thatBytes.Length; i++)
            {
                if (thisBytes[i] != thatBytes[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Calculates the width & height of an image, tries to read only the few bytes of the image to figure out image size
        /// </summary>
        /// <param name="url">Absolute path of image</param>
        /// <returns>Width and Height of image</returns>
        [System.Web.Http.HttpGet]
        public ImageInfo Image(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = @"Mozilla/5.0 (Windows NT 6.2; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/32.0.1667.0 Safari/537.36";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    using (BinaryReader reader = new BinaryReader(response.GetResponseStream()))
                    {
                        var decoders = new Dictionary<byte[][], Func<BinaryReader, ImageInfo>>()
                        {
                            //  Bitmap
                            { new byte[][]
                                {
                                    new byte[]{ 0x42, 0x4D }
                                }, new Func<BinaryReader, ImageInfo>((BinaryReader myreader) =>
                                {
                                    myreader.ReadBytes(16);
                                    return new ImageInfo()
                                    {
                                        width = myreader.ReadInt32(),
                                        height = myreader.ReadInt32(),
                                        format = "bitmap",
                                        status = HttpStatusCode.OK
                                    };
                                })
                            },

                            //  Gif
                            { new byte[][]
                                {
                                    new byte[]{ 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 },
                                    new byte[]{ 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }
                                }, new Func<BinaryReader, ImageInfo>((BinaryReader myreader) =>
                                {
                                    return new ImageInfo()
                                    {
                                        width = myreader.ReadInt16(),
                                        height = myreader.ReadInt16(),
                                        format = "gif",
                                        status = HttpStatusCode.OK
                                    };
                                })
                            },     

                            //  Png
                            { new byte[][]
                                {
                                    new byte[]{ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
                                }, new Func<BinaryReader, ImageInfo>((BinaryReader myreader) =>
                                {
                                    myreader.ReadBytes(8);
                                    return new ImageInfo()
                                    {
                                        width = ReadLittleEndianInt32(myreader),
                                        height = ReadLittleEndianInt32(myreader),
                                        format = "png",
                                        status = HttpStatusCode.OK
                                    };
                                })
                            },

                            //  Jpg
                            { new byte[][]
                                {
                                    new byte[]{ 0xff, 0xd8 },
                                }, new Func<BinaryReader, ImageInfo>((BinaryReader myreader) =>
                                {
                                    while (myreader.ReadByte() == 0xff)
                                    {
                                        byte marker = myreader.ReadByte();
                                        short chunkLength = ReadLittleEndianInt16(myreader);

                                        if (marker == 0xc0)
                                        {
                                            myreader.ReadByte();
                                            return new ImageInfo()
                                            {
                                                height = ReadLittleEndianInt16(myreader),
                                                width = ReadLittleEndianInt16(myreader),
                                                format = "jpg",
                                                status = HttpStatusCode.OK
                                            };
                                        }

                                        myreader.ReadBytes(chunkLength - 2);
                                    }
                                    return new ImageInfo()
                                    {
                                        format = "jpg",
                                        error = "Invalid jpg",
                                        status = HttpStatusCode.BadRequest
                                    };
                                })
                            }
                        };

                        var magicBytes = new byte[8];
                        for (int i = 0; i != magicBytes.Length; i++)
                        {
                            magicBytes[i] = reader.ReadByte();

                            foreach (var decoder in decoders)
                            {
                                foreach (var magic in decoder.Key)
                                {
                                    if (i + 1 == magic.Length && Match(magicBytes, magic))
                                    {
                                        return decoder.Value(reader);
                                    }
                                }
                            }
                        }
                        return new ImageInfo()
                        {
                            error = "Not a supported image type",
                            status = HttpStatusCode.UnsupportedMediaType
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ImageInfo()
                {
                    error = ex.Message,
                    status = HttpStatusCode.NotFound
                };
            }
        }

        [System.Web.Http.HttpGet]
        public string ConvertCoordinateSystem(string sourceId, string sourceDatum, string destinationId)
        {
            var source = Models.Position.Create(sourceId);
            if (source == null || !source.TryParse(sourceDatum))
            {
                return null;
            }
            var wgs84 = source.ToWgs84();
            var destination = Models.Position.Create(destinationId);
            destination.FromWgs84(wgs84);
            return destination._datum.ToString();
        }
    }
}
