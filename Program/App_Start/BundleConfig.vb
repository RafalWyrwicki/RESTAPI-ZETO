Imports System.Web
Imports System.Web.Optimization

Public Module BundleConfig
	' Aby uzyskać więcej informacji o grupowaniu, odwiedź stronę https://go.microsoft.com/fwlink/?LinkId=301862
	Public Sub RegisterBundles(ByVal bundles As BundleCollection)
		bundles.Add(New ScriptBundle("~/bundles/jquery").Include(
				   "~/Scripts/jquery-{version}.js"))

		' Użyj wersji deweloperskiej biblioteki Modernizr do nauki i opracowywania rozwiązań. Następnie, kiedy wszystko będzie
		' gotowe do produkcji, użyj narzędzia do kompilowania ze strony https://modernizr.com, aby wybrać wyłącznie potrzebne testy.
		bundles.Add(New ScriptBundle("~/bundles/modernizr").Include(
					"~/Scripts/modernizr-*"))

		bundles.Add(New Bundle("~/bundles/bootstrap").Include(
					"~/Scripts/bootstrap.js"))

		bundles.Add(New StyleBundle("~/Content/css").Include(
					"~/Content/bootstrap.css",
					"~/Content/site.css"))
	End Sub
End Module