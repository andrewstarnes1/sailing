#if UNITY_EDITOR
using System;
using UnityEngine;

using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

using UnityEditor.AssetImporters;
using UnityEditor;

namespace KWS
{
    [ScriptedImporter(1, "kwsTexture")]
    internal class TextureImporter : ScriptedImporter
    {

        public static TextureFormat GetCompatibleTextureFormat(BuildTarget target, KW_Extensions.UsedChannels usedChannels, bool isHDR)
        {
            if (isHDR)
            {
                return usedChannels switch
                {
                    KW_Extensions.UsedChannels._R => (IsMobileTarget(target) ? TextureFormat.ASTC_HDR_8x8 : TextureFormat.BC6H),
                    KW_Extensions.UsedChannels._RG => (IsMobileTarget(target) ? TextureFormat.ASTC_HDR_8x8 : TextureFormat.BC6H),
                    KW_Extensions.UsedChannels._RGB => (IsMobileTarget(target) ? TextureFormat.ASTC_HDR_8x8 : TextureFormat.BC6H),
                    KW_Extensions.UsedChannels._RGBA => (IsMobileTarget(target) ? TextureFormat.ASTC_HDR_8x8 : TextureFormat.RGBAHalf),
                    _ => throw new ArgumentOutOfRangeException(nameof(usedChannels), usedChannels, null)
                };
            }
            else
            {
                return usedChannels switch
                {
                    KW_Extensions.UsedChannels._R => (IsMobileTarget(target) ? TextureFormat.EAC_R : TextureFormat.BC4),
                    KW_Extensions.UsedChannels._RG => (IsMobileTarget(target) ? TextureFormat.EAC_RG : TextureFormat.BC5),
                    KW_Extensions.UsedChannels._RGB => (IsMobileTarget(target) ? TextureFormat.ASTC_8x8 : TextureFormat.BC7),
                    KW_Extensions.UsedChannels._RGBA => (IsMobileTarget(target) ? TextureFormat.ASTC_8x8 : TextureFormat.BC7),
                    _ => throw new ArgumentOutOfRangeException(nameof(usedChannels), usedChannels, null)
                };
            }
        }

        static bool IsMobileTarget(BuildTarget target)
        {
            if (target == BuildTarget.Android || target == BuildTarget.Switch || target == BuildTarget.iOS) return true;
            else return false;
        }

        public static void Write(Texture2D r, string path, bool useAutomaticCompressionFormat, KW_Extensions.UsedChannels usedChannels = default, bool isHDR = false, bool mipChain = false)
        {
            var fullName = path + ".kwsTexture";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullName));
            }

            using (FileStream fs = new FileStream(path + ".kwsTexture", FileMode.Create))
            using (GZipStream zipStream = new GZipStream(fs, CompressionMode.Compress, false))
            using (var bw = new BinaryWriter(zipStream))
            {
                var bytes = r.GetRawTextureData();

                bw.Write(0);
                bw.Write(useAutomaticCompressionFormat);
                bw.Write((int)usedChannels);
                bw.Write(isHDR);
                bw.Write((int)r.format);
                bw.Write(r.width);
                bw.Write(r.height);
                bw.Write(mipChain);
                bw.Write((int)r.wrapMode);
                bw.Write((int)r.filterMode);
                bw.Write(bytes.Length);
                bw.Write(bytes);



            }

            AssetDatabase.SaveAssets();
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            using (var zipFile = File.Open(ctx.assetPath, FileMode.Open))
            using (GZipStream zipStream = new GZipStream(zipFile, CompressionMode.Decompress))
            using (var r = new BinaryReader(zipStream))
            {
                var version = r.ReadInt32();
                if (version != 0)
                {
                    Debug.LogError("Version mismatch in kwsTexture aseset");
                    return;
                }

                var useAutomaticCompressionFormat = r.ReadBoolean();
                var usedChannels = (KW_Extensions.UsedChannels)r.ReadInt32();
                var isHDR = r.ReadBoolean();
                var format = (TextureFormat)r.ReadInt32();
                var width = r.ReadInt32();
                var height = r.ReadInt32();
                var mipChain = r.ReadBoolean();
                var wrapMode = (TextureWrapMode)(r.ReadInt32());
                var filterMode = (FilterMode)(r.ReadInt32());
                int length = r.ReadInt32();
                var bytes = r.ReadBytes(length);

                var tex = new Texture2D(width, height, format, mipChain, true);
                tex.wrapMode = wrapMode;
                tex.filterMode = filterMode;
                tex.LoadRawTextureData(bytes);
                tex.Apply();

                if (useAutomaticCompressionFormat)
                {
                    var targetFormat = GetCompatibleTextureFormat(ctx.selectedBuildTarget, usedChannels, isHDR);
                    EditorUtility.CompressTexture(tex, targetFormat, TextureCompressionQuality.Best);
                }

                string waterID = new DirectoryInfo(ctx.assetPath).Parent.Name;
                ctx.AddObjectToAsset(waterID, tex);
                ctx.SetMainObject(tex);
            }
        }
    }

}
#endif