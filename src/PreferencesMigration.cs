using System;
using System.IO;

namespace NetSpeck {
internal static class PreferencesMigration {
    // Copy preferences and imported assets once. Leave the original data intact.
    public static void CopyIfNeeded(string previous,string current) {
        string oldSettings=Path.Combine(previous,"settings.ini"),newSettings=Path.Combine(current,"settings.ini");
        if(File.Exists(newSettings)||!File.Exists(oldSettings))return;
        if((File.GetAttributes(previous)&FileAttributes.ReparsePoint)!=0)return;
        Directory.CreateDirectory(current);
        foreach(string library in new[]{"Fonts","Icons"}) {
            string source=Path.Combine(previous,library),target=Path.Combine(current,library);
            if(!Directory.Exists(source)||(File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)continue;
            foreach(string file in Directory.GetFiles(source)) {
                string ext=Path.GetExtension(file).ToLowerInvariant();
                bool allowed=library=="Fonts"?(ext==".ttf"||ext==".ttc"||ext==".otf"):(ext==".ico"||ext==".png");
                if(!allowed||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                Directory.CreateDirectory(target);
                string destination=Path.Combine(target,Path.GetFileName(file));
                if(!File.Exists(destination))File.Copy(file,destination);
            }
        }
        // Settings are the completion marker: an interrupted copy can be retried.
        File.Copy(oldSettings,newSettings);
    }
}
}
