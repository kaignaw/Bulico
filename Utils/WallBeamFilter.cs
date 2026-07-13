using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;

namespace Bulico
{
    public class WallBeamFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is Wall)
                return true;

            FamilyInstance fi = elem as FamilyInstance;
            if (fi != null && fi.StructuralType == StructuralType.Beam)
                return true;

            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
