using Autodesk.Revit.DB;

namespace Bulico
{
    /// <summary>
    /// 生成与文档绑定的缓存键。ElementId 只在所属文档内有效，
    /// 跨文档复用上一次选择的类型会导致类型/标高错配。
    /// </summary>
    internal static class DocumentScope
    {
        public static string Key(Document doc)
        {
            if (doc == null)
                return string.Empty;

            string path = doc.PathName;
            if (string.IsNullOrEmpty(path))
                path = doc.Title;
            if (path == null)
                path = string.Empty;

            return path + "|" + doc.GetHashCode().ToString();
        }
    }
}
