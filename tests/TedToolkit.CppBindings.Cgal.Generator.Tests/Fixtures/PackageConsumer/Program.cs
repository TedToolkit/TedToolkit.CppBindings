using TedToolkit.CppBindings.Cgal.Generator;

var profile = CgalProfileManifest.LoadDefault();
if (profile.ProfileId != "epick-windows-v1" || profile.CgalVersion != "6.2" || profile.Declarations.Count != 19)
{
    return 1;
}

return 0;
