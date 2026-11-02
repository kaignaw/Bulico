using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Bulico
{
    /// <summary>
    /// 按“命令 + 文档”记住上一次的楼板类型、标高与偏移。
    /// 只缓存 ElementId，切换文档或元素被删除时自然失效，不会外部持有 Element。
    /// </summary>
    internal static class FloorSettingsCache
    {
        private sealed class Entry
        {
            public string DocKey;
            public ElementId FloorTypeId;
            public ElementId LevelId;
            public double OffsetMeters;
            public bool UseCenterline = true;
        }

        private static readonly Dictionary<string, Entry> Entries =
            new Dictionary<string, Entry>();

        public static bool TryGetInitial(
            string commandKey,
            Document doc,
            out ElementId floorTypeId,
            out ElementId levelId,
            out double offsetMeters,
            out bool useCenterline)
        {
            floorTypeId = null;
            levelId = null;
            offsetMeters = 0.0;
            useCenterline = true;

            Entry entry;
            if (!Entries.TryGetValue(commandKey, out entry))
                return false;

            if (entry.DocKey != DocumentScope.Key(doc))
                return false;

            floorTypeId = entry.FloorTypeId;
            levelId = entry.LevelId;
            offsetMeters = entry.OffsetMeters;
            useCenterline = entry.UseCenterline;
            return true;
        }

        public static void Store(string commandKey, Document doc, FloorSettings settings)
        {
            if (settings == null || settings.FloorType == null || settings.Level == null)
                return;

            Entries[commandKey] = new Entry
            {
                DocKey = DocumentScope.Key(doc),
                FloorTypeId = settings.FloorType.Id,
                LevelId = settings.Level.Id,
                OffsetMeters = settings.OffsetMeters,
                UseCenterline = settings.UseCenterline
            };
        }
    }
}
