using System;
using System.IO;
using System.IO.Compression;
using IntelOrca.Biohazard.BioRand.Extensions;
using IntelOrca.Biohazard.REE.Messages;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.BioRand.REE.Extensions
{
    public static class PatchContextExtensions
    {
        public static void SetFile(this IReeRandomizerContext context, string path, ReadOnlyMemory<byte> data) =>
            context.SetFile(path, data.ToArray());

        public static bool Exists(this IReeRandomizerContext context, string path) => context.TryGetFile(path) != null;

        public static byte[] GetFile(this IReeRandomizerContext context, string path)
        {
            var data = context.TryGetFile(path) ?? throw new RandomizerUserException($"Unable to read '{path}'");
            return data;
        }

        public static PfbFile GetPfbFile(this IReeRandomizerContext context, string path)
        {
            return new PfbFile(17, GetFile(context, path));
        }

        public static void ModifyPfbFile(this IReeRandomizerContext context, string path, Func<RszScene, RszScene> callback)
        {
            var pfbFile = context.GetPfbFile(path).ToBuilder(context.TypeRepository);
            pfbFile.Scene = callback(pfbFile.Scene);
            context.SetPfbFile(path, pfbFile.AddMissingResources().Build());
        }

        public static void SetPfbFile(this IReeRandomizerContext context, string path, PfbFile value)
        {
            context.SetFile(path, value.Data);
        }

        public static ScnFile GetScnFile(this IReeRandomizerContext context, string path)
        {
            return new ScnFile(20, GetFile(context, path));
        }

        public static void ModifyScnFile(this IReeRandomizerContext context, string path, Func<RszScene, RszScene> callback)
        {
            var scnFile = context.GetScnFile(path).ToBuilder(context.TypeRepository);
            scnFile.Scene = callback(scnFile.Scene);
            context.SetScnFile(path, scnFile.AddMissingResources().Build());
        }

        public static void SetScnFile(this IReeRandomizerContext context, string path, ScnFile value)
        {
            context.SetFile(path, value.Data);
        }

        public static UserFile GetUserFile(this IReeRandomizerContext context, string path)
        {
            return new UserFile(GetFile(context, path));
        }

        public static T DeserializeUserFile<T>(this IReeRandomizerContext context, string path)
        {
            var userFile = context.GetUserFile(path);
            return RszSerializer.Deserialize<T>(userFile.GetObjects(context.TypeRepository)[0])!;
        }

        public static void SerializeUserFile<T>(this IReeRandomizerContext context, string path, T value)
        {
            var userFile = context.GetUserFile(path);
            var builder = userFile.ToBuilder(context.TypeRepository);
            var targetType = builder.Objects[0].Type;
            builder.Objects = [(RszObjectNode)RszSerializer.Serialize(targetType, value!)];
            context.SetUserFile(path, builder.Build());
        }

        public static void SetUserFile(this IReeRandomizerContext context, string path, UserFile value)
        {
            context.SetFile(path, value.Data);
        }

        public static void ModifyUserFile(this IReeRandomizerContext context, string path, Func<RszObjectNode, RszObjectNode> callback)
        {
            var userFile = context.GetUserFile(path);
            var builder = userFile.ToBuilder(context.TypeRepository);
            builder.Objects = [callback(builder.Objects[0])];
            context.SetUserFile(path, builder.Build());
        }

        public static void ModifyUserFile<T>(this IReeRandomizerContext context, string path, Func<T, T> callback)
        {
            SerializeUserFile(context, path, callback(DeserializeUserFile<T>(context, path)));
        }

        public static MsgFile GetMsgFile(this IReeRandomizerContext context, string path)
        {
            return new MsgFile(GetFile(context, path));
        }

        public static void SetMsgFile(this IReeRandomizerContext context, string path, MsgFile msg)
        {
            context.SetFile(path, msg.Data.ToArray());
        }

        public static void ModifyMsgFile(this IReeRandomizerContext context, string path, Action<MsgFile.Builder> callback)
        {
            var msgFile = context.GetMsgFile(path);
            var builder = msgFile.ToBuilder();
            callback(builder);
            context.SetMsgFile(path, builder.Build());
        }

        public static void ApplyOverlay(this IReeRandomizerContext context, byte[] zipData)
        {
            var supplementZip = new ZipArchive(new MemoryStream(zipData));
            foreach (var entry in supplementZip.Entries)
            {
                if (entry.Length == 0)
                    continue;

                var data = entry.GetData();
                context.SetFile(entry.FullName, data);
            }
        }
    }
}
