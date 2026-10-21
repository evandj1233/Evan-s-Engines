"""Make a local Windows runtime from an existing licensed CPython installation.

Run with the Python version used to install Runtime/Lib dependencies. It copies
the interpreter and standard library without the source installation's third-
party site-packages, user files, caches, credentials or development environment.
"""
import json
import pathlib
import shutil
import sys
import sysconfig
import zipfile

here=pathlib.Path(__file__).resolve().parent
root=here.parent if here.name=="Source" else here
destination=root/"Runtime"/"Python"
destination.mkdir(parents=True,exist_ok=True)
source=pathlib.Path(sys.executable).parent
for path in source.iterdir():
    if path.is_file() and (path.suffix.lower()==".dll" or path.name in ("python.exe","pythonw.exe","LICENSE.txt")):
        shutil.copy2(path,destination/path.name)
if (source/"DLLs").is_dir():shutil.copytree(source/"DLLs",destination/"DLLs",dirs_exist_ok=True)
stdlib=pathlib.Path(sysconfig.get_path("stdlib"))
version=f"python{sys.version_info.major}{sys.version_info.minor}"
exclude={"site-packages","test","tests","__pycache__","idlelib","tkinter","turtledemo","ensurepip"}
with zipfile.ZipFile(destination/(version+".zip"),"w",zipfile.ZIP_DEFLATED) as archive:
    for path in stdlib.rglob("*"):
        relative=path.relative_to(stdlib)
        if path.is_file() and not any(part in exclude for part in relative.parts) and path.suffix in (".py",".json",".txt"):
            archive.write(path,relative.as_posix())
(destination/(version+"._pth")).write_text(version+".zip\nDLLs\n..\\Lib\nimport site\n")
(root/"runtime.json").write_text(json.dumps({"Python":"Runtime\\Python\\python.exe"},indent=2))
print("Packaged local CPython",sys.version.split()[0])
