using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Bulico
{
    internal static class CommandGuard
    {
        public static bool TryGetDocument(
            ExternalCommandData commandData,
            string title,
            out UIDocument uidoc,
            out Document doc)
        {
            uidoc = commandData?.Application?.ActiveUIDocument;
            doc = uidoc?.Document;

            if (doc != null)
                return true;

            TaskDialog.Show(title, "请先打开一个 Revit 项目。");
            return false;
        }
    }
}
