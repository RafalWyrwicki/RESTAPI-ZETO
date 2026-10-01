Imports System.ComponentModel.DataAnnotations

Namespace Models
    Public Class DaneRequest
        Public Const MaxWydzialLength As Integer = 20
        Public Const MaxRepertoriumLength As Integer = 7
        Public Const MaxTomLength As Integer = 8
        Public Const MaxIpAddressLength As Integer = 45
        Public Const MaxSignatureLength As Integer = 200

        <Required(ErrorMessage:="Pole Identyfikator jest wymagane."),
         Range(1, 9999, ErrorMessage:="Identyfikator - błędny format danych")>
        Public Property Identyfikator As Nullable(Of Integer)

        <Required(ErrorMessage:="Pole Wydział jest wymagane."),
         StringLength(MaxWydzialLength, ErrorMessage:="Pole Wydział jest zbyt długie.")>
        Public Property Wydzial As String

        <Range(0, 255, ErrorMessage:="Sekcja - dozwolone liczby od 0 do 255.")>
        Public Property Sekcja As Nullable(Of Integer)

        <Required(ErrorMessage:="Pole Repertorium jest wymagane."),
         StringLength(MaxRepertoriumLength, ErrorMessage:="Pole Repertorium jest zbyt długie.")>
        Public Property Repertorium As String

        <Required(ErrorMessage:="Pole Nr_Sprawy jest wymagane."),
         Range(1, 99999, ErrorMessage:="Nr_Sprawy - błędny format danych - dozwolone liczby od 1 do 99999")>
        Public Property Nr_Sprawy As Nullable(Of Integer)

        <Required(ErrorMessage:="Pole Rok jest wymagane."),
         Range(0, 9999, ErrorMessage:="Rok - dozwolona wartość od 0 do 9999.")>
        Public Property Rok As Nullable(Of Integer)

        <StringLength(MaxTomLength, ErrorMessage:="Pole Tom może mieć maksymalnie 8 znaków ASCII.")>
        Public Property Tom As String

        <Required(ErrorMessage:="Pole IP_Drukarka jest wymagane."),
         StringLength(MaxIpAddressLength, ErrorMessage:="Pole IP_Drukarka jest zbyt długie.")>
        Public Property IP_Drukarka As String

        <Required(ErrorMessage:="Pole Sygnatura_Text jest wymagane."),
         StringLength(MaxSignatureLength, ErrorMessage:="Pole Sygnatura_Text jest zbyt długie.")>
        Public Property Sygnatura_Text As String

        <Required(ErrorMessage:="Pole Sygnatura_Barcode jest wymagane."),
         StringLength(MaxSignatureLength, ErrorMessage:="Pole Sygnatura_Barcode jest zbyt długie.")>
        Public Property Sygnatura_Barcode As String
    End Class
End Namespace
