Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading

Module Module1
    Sub Main()
        Initaliaze()

        Run()
    End Sub

    Sub Initaliaze()
        If IO.File.Exists("settings.ini") Then
            Console.WriteLine("Loading settings.ini")

            LogMeIn.Settings = Serialization.Deserialize(IO.File.ReadAllText("settings.ini"), GetType(Settings))
        Else
            Console.WriteLine("settings.ini not found. Creating new.")

            LogMeIn.Settings = New Settings

            IO.File.WriteAllText("settings.ini", Serialization.Serialize(LogMeIn.Settings, GetType(Settings)))
        End If

        If IO.File.Exists("logo.png") Then
            Console.WriteLine("Loading logo.png")

            LogMeIn.Picture = IO.File.ReadAllBytes("logo.png")
        Else
            Console.WriteLine("logo.png not found")
        End If

        If IO.File.Exists("users.dat") Then
            Console.WriteLine("Loading users.dat")

            LogMeIn.Users = Serialization.Deserialize(IO.File.ReadAllText("users.dat"), GetType(List(Of User)))
        Else
            Console.WriteLine("users.dat not found. Creating new.")

            LogMeIn.Users = New List(Of User)

            IO.File.WriteAllText("users.dat", Serialization.Serialize(LogMeIn.Users, GetType(List(Of User))))
        End If
    End Sub

    Sub Run()
        Dim Server = New TcpListener(IPAddress.Parse(LogMeIn.Settings.LocalAddress), LogMeIn.Settings.Port)
        Server.Start()

        Try
            While True
                Dim TcpClient As TcpClient = Server.AcceptTcpClient()

                Dim Client As New Client(TcpClient)
            End While
        Catch e As Exception
            Console.WriteLine("Something went wrong: " & e.ToString)
        End Try
    End Sub
End Module

<Serializable> Public Class Settings
    Public DataPath As String = "C:\logindata\"

    Public Port As Integer = 3000
    Public LocalAddress As String = "127.0.0.1"

    Public CurrentID As ULong = 0

    Public Name As String = "Test server"
End Class

Class LogMeIn
    Public Shared Users As List(Of User)

    Public Shared Picture As Byte()

    Public Shared Settings As Settings
End Class

Class Client
#Region "Fields"
    Dim TcpClient As TcpClient

    Dim TcpStream As NetworkStream
    Dim TcpWriter As BinaryWriter
    Dim TcpReader As BinaryReader

    Dim User As User
#End Region

#Region "Constructor"
    Sub New(Client As TcpClient)
        TcpClient = Client

        TcpStream = TcpClient.GetStream()

        TcpWriter = New BinaryWriter(TcpStream)
        TcpReader = New BinaryReader(TcpStream)

        Dim MyThread As Thread = New Thread(AddressOf Speak)
        MyThread.Start()
    End Sub
#End Region

#Region "Main"
    Sub Speak()
        Dim command As String

        Do
            Try
                command = ReceiveMessage()

                CallByName(Me, command, CallType.Method)
            Catch e As EndOfStreamException
                Console.WriteLine("Client Disconnected")
            Catch e As Exception
                Console.WriteLine("Something went wrong")
            End Try
        Loop
    End Sub
#End Region

#Region "Logic"
#Region "FileSystem"
    Sub GetFilesList()
        Dim FilesList As String() = System.IO.Directory.GetFiles(LogMeIn.Settings.DataPath & User.ID, "*", System.IO.SearchOption.AllDirectories)

        For i As Integer = 0 To FilesList.Length - 1
            FilesList(i) = FilesList(i).Replace(LogMeIn.Settings.DataPath & User.ID & "\", "")
        Next

        'If FilesList.Length = 0 Then FilesList = {"null"}
        SendMessage(Serialization.Serialize(FilesList, GetType(String())))
    End Sub

    Sub UploadFile()
        Dim RelativePath As String = ReceiveMessage()
        Dim File As Byte() = ReceiveFile()
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(LogMeIn.Settings.DataPath & User.ID & "\" & RelativePath))
        IO.File.WriteAllBytes(LogMeIn.Settings.DataPath & User.ID & "\" & RelativePath, File)
    End Sub

    Sub DownloadFile()
        Dim RelativePath As String = ReceiveMessage()

        SendFile(IO.File.ReadAllBytes(LogMeIn.Settings.DataPath & User.ID & "\" & RelativePath))
    End Sub

    Sub DeleteFiles()

    End Sub
#End Region

#Region "Users"
    Sub CreateUser()
        User = New User(ReceiveMessage(), ReceiveMessage(), ReceiveMessage(), ReceiveMessage(), ReceiveMessage(), ReceiveMessage())

        If Not LogMeIn.Users.Contains(User) Then
            IO.Directory.CreateDirectory(LogMeIn.Settings.DataPath & User.ID)

            LogMeIn.Users.Add(User)

            IO.File.WriteAllText("users.dat", Serialization.Serialize(LogMeIn.Users, GetType(List(Of User))))

            SendMessage("Y")
        Else
            User = Nothing

            SendMessage("N")
        End If
    End Sub

    Sub LoginUser()
        User = New User(ReceiveMessage(), ReceiveMessage())

        If LogMeIn.Users.Contains(User) Then
            User = LogMeIn.Users(LogMeIn.Users.IndexOf(User))
            SendMessage("Y")

            SendMessage(Serialization.Serialize(User, GetType(User)))
        Else
            SendMessage("N")
        End If
    End Sub
#End Region

#Region "Other"
    Sub GetPicture()
        SendFile(LogMeIn.Picture)
    End Sub

    Sub GetName()
        SendMessage(LogMeIn.Settings.Name)
    End Sub
#End Region
#End Region

#Region "Messaging"
    Sub SendMessage(message As String)
        Dim bytes As Byte() = Encoding.Unicode.GetBytes(message)

        TcpWriter.Write(bytes.Length)

        TcpWriter.Write(bytes)
    End Sub
    Function ReceiveMessage() As String
        Dim messageLength As Integer = TcpReader.ReadInt32
        Dim messageData() As Byte = TcpReader.ReadBytes(messageLength)
        Dim message As String = Encoding.Unicode.GetString(messageData)

        Return message
    End Function

    Sub SendFile(file As Byte())
        Dim bytes As Byte() = file

        TcpWriter.Write(bytes.Length)

        TcpWriter.Write(bytes)
    End Sub
    Function ReceiveFile() As Byte()
        Dim fileLength As Integer = TcpReader.ReadInt32
        Dim file() As Byte = TcpReader.ReadBytes(fileLength)

        Return file
    End Function
#End Region
End Class

Class Serialization
    Public Shared Function Serialize(Obj As Object, Typ As Type) As String
        Dim xs As New System.Xml.Serialization.XmlSerializer(Typ)
        Dim w As New IO.StringWriter()
        xs.Serialize(w, Obj)

        Return w.ToString
    End Function

    Public Shared Function Deserialize(Xml As String, T As Type) As Object
        Dim serializer As New System.Xml.Serialization.XmlSerializer(T)
        Using r As TextReader = New StringReader(Xml)
            Return serializer.Deserialize(r)
        End Using
    End Function
End Class

<Serializable> Public Class User
#Region "Fields"
    Public Username As String
    Public Password As String
    Public Name As String
    Public Surname As String
    Public UserClass As String
    Public UserParallel As String

    Public ID As ULong
#End Region

#Region "Constructors"
    Public Sub New(Username, Password, Name, Surname, UserClass, UserParallel)
        Me.Username = Username
        Me.Password = Password
        Me.Name = Name
        Me.Surname = Surname
        Me.UserClass = UserClass
        Me.UserParallel = UserParallel

        Me.ID = LogMeIn.Settings.CurrentID
        LogMeIn.Settings.CurrentID += 1
    End Sub

    Public Sub New(Username, Password)
        Me.Username = Username
        Me.Password = Password
    End Sub

    Public Sub New()

    End Sub
#End Region

    Public Overrides Function Equals(obj As Object) As Boolean
        Return obj.Username = Username And obj.Password = Password
    End Function
End Class
