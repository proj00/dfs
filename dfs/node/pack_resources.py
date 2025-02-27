import xml.etree.ElementTree as et
import os
import subprocess

# for future reference:

# <data name="index.html" type="System.Resources.ResXFileRef, System.Windows.Forms">
#   <value>UiResources\index.html;System.Byte[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
# </data>


def main():
    subprocess.run(["npm", "run", "build", "--prefix", "./../ui"], shell=True)
    stuffToAdd = []
    for folder, subs, files in os.walk(".\\UiResources"):
        for file in files:
            stuffToAdd.append(folder[2:] + "/" + file)

    tree = et.parse("UiResources.resx")
    root = tree.getroot()

    # clean up previous stuff
    for child in root.findall("data"):
        root.remove(child)

    for file in stuffToAdd:
        data = et.Element(
            "data",
            {
                "name": file[12:],
                "type": "System.Resources.ResXFileRef, System.Windows.Forms",
            },
        )

        value = et.Element("value")
        value.text = (
            file
            + ";System.Byte[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        )

        data.append(value)
        root.append(data)

    et.indent(root, space="\t", level=0)
    tree.write("UiResources.resx", encoding="utf-8")


if __name__ == "__main__":
    main()
