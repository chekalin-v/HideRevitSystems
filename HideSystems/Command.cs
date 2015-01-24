#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using OperationCanceledException = Autodesk.Revit.Exceptions.OperationCanceledException;

#endregion

namespace HideSystems
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            Reference r;
            try
            {
                r = uidoc.Selection.PickObject(ObjectType.Element,
                    new SystemElementFilter(),
                    "Select an alement of a system");
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;

            }

            var elem = doc.GetElement(r.ElementId);

            var systemNameParam =
                elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            if (systemNameParam == null)
            {
                message = "How did you do that?";
                return Result.Failed;
            }

            var view = doc.ActiveView;

            var categoriesWithSystem =
                GetCategoriesApplicableForParameter(doc, BuiltInParameter.RBS_SYSTEM_NAME_PARAM);

            var categoriesWithoutSystemNameParameter =
                GetCategoriesApplicableForParameter(doc, BuiltInParameter.RBS_SYSTEM_NAME_PARAM, true);

            var systemName = systemNameParam.AsString();

            // An element can be assigned to the several systems
            // In this case System Name parameter has a comma separated
            // list of the systems
            // Create several rules            
            IList<FilterRule> rules = new List<FilterRule>();

            var systems = systemName.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var system in systems)
            {
                rules.Add(ParameterFilterRuleFactory
            .CreateNotContainsRule(new ElementId(BuiltInParameter.RBS_SYSTEM_NAME_PARAM),
            system.Trim(), true));
            }


            using (var t = new Transaction(doc, "System isolate"))
            {
                t.Start();

                // Hide all elements which do no have a System Name parameter
                // Do not use this filter if you want to hide only the systems                
                ParameterFilterElement filter1 =
                    ParameterFilterElement.Create(doc,
                        "All elements without System Name parameter",
                        categoriesWithoutSystemNameParameter);

                view.AddFilter(filter1.Id); // добавл€ем фильтр на вид
                view.SetFilterVisibility(filter1.Id, false); // и пр€чем элементы


                // Hide elements which are not in the selected system                
                ParameterFilterElement filter2 =
                    ParameterFilterElement.Create(doc,
                        string.Format("All elements not in the systems {0}", systemName),
                        categoriesWithSystem,
                        rules);

                view.AddFilter(filter2.Id);
                view.SetFilterVisibility(filter2.Id, false);

                t.Commit();
            }

            return Result.Succeeded;

        }

/// <summary>        
/// Returns the list of the categories what can be used in filter 
/// by the specific parameter
/// </summary>
/// <param name="doc">Document on which the filter is applying</param>
/// <param name="bip">BuiltInParameter</param>
/// <param name="inverse">If true, the list will be inverted. I.e. you will get 
/// the list of the categories what cannot be used in filter 
/// by the specific parameter </param>
/// <returns></returns>
ICollection<ElementId> GetCategoriesApplicableForParameter(Document doc,
    BuiltInParameter bip,
    bool inverse = false)
{
    // All categories available for filter
    var allCategories = ParameterFilterUtilities.GetAllFilterableCategories();

    ICollection<ElementId> retResult = new List<ElementId>();

            
    foreach (ElementId categoryId in allCategories)
    {
        // get the list of paramteres, compatible with the category.                
        var applicableParameters =
            ParameterFilterUtilities.GetFilterableParametersInCommon(doc, new[] { categoryId });

        // if the parameter we are interested in the collection
        // add it to the result
        if (applicableParameters.Contains(new ElementId(bip)))
        {
            retResult.Add(categoryId);
        }
    }

    // Invert if needed. 
    if (inverse)
        retResult =
            allCategories.Where(x => !retResult.Contains(x)).ToList();

    return retResult;
}
    }

    /// <summary>
    /// Allow to select only element what has the parameter 
    /// 'System Name' and has value.
    /// </summary>
    public class SystemElementFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            var systemNameParam =
                elem.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            return systemNameParam != null && systemNameParam.HasValue;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            throw new NotImplementedException();
        }
    }
}
