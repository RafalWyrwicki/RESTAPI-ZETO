Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.Http
Imports REST_API.Filters
Imports REST_API.Handlers

Public Module WebApiConfig
	Public Sub Register(ByVal config As HttpConfiguration)
		' Konfiguracja i usługi składnika Web API
		config.MessageHandlers.Add(New RequestProtectionHandler())
		config.Filters.Add(New GlobalApiExceptionFilterAttribute())

		' Trasy składnika Web API
		config.MapHttpAttributeRoutes()
	End Sub
End Module
