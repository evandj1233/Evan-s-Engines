using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

// Optional read-only helper. Requires the user's licensed SOLIDWORKS installation.
// Never starts CAD, rebuilds, changes material, moves components or saves files.
public static class CadReadback {
 [STAThread]public static int Main(string[] args){string root=AppDomain.CurrentDomain.BaseDirectory;try{
  SldWorks sw=(SldWorks)Marshal.GetActiveObject("SldWorks.Application");ModelDoc2 doc=sw.ActiveDoc as ModelDoc2;if(doc==null)throw new Exception("Open a SOLIDWORKS part or assembly first.");
  MassProperty mass=doc.Extension.CreateMassProperty() as MassProperty;if(mass==null)throw new Exception("SOLIDWORKS could not calculate the current document mass properties.");
  string database="",material="";if(doc is PartDoc)material=((PartDoc)doc).GetMaterialPropertyName2("",out database);
  var data=new Dictionary<string,object>{{"document",doc.GetTitle()},{"configuration",doc.ConfigurationManager.ActiveConfiguration.Name},{"mass_kg",mass.Mass},{"volume_m3",mass.Volume},{"surface_area_m2",mass.SurfaceArea},{"material",material},{"material_database",Path.GetFileName(database)},{"units","SI"},{"read_only",true},{"note","Mass depends on assigned CAD material and current configuration; this is not a structural or power validation."}};
  string target=Path.Combine(root,"Runs","SOLIDWORKS_Readback.json");Directory.CreateDirectory(Path.GetDirectoryName(target));File.WriteAllText(target,new JavaScriptSerializer().Serialize(data));return 0;
 }catch(Exception e){Directory.CreateDirectory(Path.Combine(root,"Evidence"));File.WriteAllText(Path.Combine(root,"Evidence","CAD_Readback_Error.txt"),e.Message);return 1;}}
}
