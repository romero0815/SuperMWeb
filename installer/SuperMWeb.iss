; installer/SuperMWeb.iss
;
; Instalador de SuperMWeb (punto de venta web) para la caja.
; Automatiza las secciones 1 a 5 de docs/despliegue.md:
;   - Copia la publicacion self-contained (no requiere .NET en la caja).
;   - Genera appsettings.Production.json con la base, el puerto, el nombre del
;     negocio y la impresora (solo en la primera instalacion).
;   - Opcional: da acceso a la base a la cuenta del servicio, en una sola
;     transaccion (o se aplica todo o nada).
;   - Registra el servicio de Windows SuperMWeb con reinicio ante fallo y lo
;     inicia, esperando a que /health responda.
;   - Opcional: backup diario adicional, accesos directos de Chrome y energia.
;
; No crea, modifica ni borra tablas, datos, procedimientos ni triggers, y no
; toca tareas de backup existentes (la caja ya tiene la del escritorio).
;
; No compilar a mano: usar installer/build-installer.ps1, que publica primero
; y le pasa a ISCC las rutas y la version.
;
; Ojo en [Code]: ninguna linea puede empezar con #13#10 (ISPP la toma como
; directiva del preprocesador).

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\SuperMWeb\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts\SuperMWeb\instalador"
#endif

#define AppName "SuperM Web POS"
#define ServiceName "SuperMWeb"
#define ExeName "SuperMWeb.exe"

[Setup]
; No cambiar el AppId: identifica la instalacion para actualizar y desinstalar.
AppId={{3F6B8E2A-9C41-4D7E-B5A2-6E1F0C8D4B97}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=SuperM
VersionInfoVersion={#AppVersion}
DefaultDirName=C:\SuperM\SuperMWeb
DefaultGroupName=SuperM
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename=SuperMWeb-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#ExeName}
SetupLogging=yes
; El servicio se detiene en PrepareToInstall; Restart Manager no hace falta.
CloseApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "permisosbd"; Description: "Dar acceso a la base de datos a la cuenta del servicio (NT AUTHORITY\SYSTEM)"; GroupDescription: "Base de datos:"
Name: "backup"; Description: "Programar un backup diario adicional, con hora configurable (requiere sqlcmd)"; GroupDescription: "Base de datos:"; Flags: unchecked
Name: "escritorio"; Description: "Acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "kiosco"; Description: "Abrir SuperM en Chrome modo kiosco al iniciar sesión (todos los usuarios)"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "energia"; Description: "Evitar que el equipo se suspenda o apague la pantalla"; GroupDescription: "Equipo:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "appsettings.Development.json,\logs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\scripts\backup-db.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\scripts\restore-db.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\scripts\programar-backup.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "superm-bd.ps1"; Flags: dontcopy

[Dirs]
Name: "{app}\logs"

[INI]
Filename: "{app}\SuperM Web POS.url"; Section: "InternetShortcut"; Key: "URL"; String: "{code:UrlApp}"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{code:RutaChrome}"; Parameters: "--app={code:UrlApp} --no-first-run"; Check: ChromeEncontrado
Name: "{group}\{#AppName}"; Filename: "{app}\SuperM Web POS.url"; Check: not ChromeEncontrado
Name: "{group}\Registros de SuperM Web"; Filename: "{app}\logs"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{code:RutaChrome}"; Parameters: "--app={code:UrlApp} --no-first-run"; Tasks: escritorio; Check: ChromeEncontrado
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\SuperM Web POS.url"; Tasks: escritorio; Check: not ChromeEncontrado
Name: "{commonstartup}\SuperM Kiosco"; Filename: "{code:RutaChrome}"; Parameters: "--kiosk --new-window --no-first-run --disable-features=Translate {code:UrlApp}"; Tasks: kiosco; Check: ChromeEncontrado
Name: "{commonstartup}\SuperM Kiosco"; Filename: "{app}\SuperM Web POS.url"; Tasks: kiosco; Check: not ChromeEncontrado

[Run]
Filename: "{code:RutaChrome}"; Parameters: "--app={code:UrlApp} --no-first-run"; Description: "Abrir {#AppName}"; Flags: postinstall nowait skipifsilent; Check: ChromeEncontrado
Filename: "{code:UrlApp}"; Description: "Abrir {#AppName}"; Flags: postinstall shellexec nowait skipifsilent; Check: not ChromeEncontrado

[UninstallDelete]
Type: files; Name: "{app}\SuperM Web POS.url"

[Code]
const
  NombreServicio = '{#ServiceName}';
  NombreTareaBackup = 'SuperM Backup Diario';
  ArchivoConfig = 'appsettings.Production.json';
  HoraBackupPorDefecto = '03:00';

var
  PaginaBD: TInputQueryWizardPage;
  PaginaCaja: TInputQueryWizardPage;
  PaginaBackup: TInputQueryWizardPage;
  EtiquetaBackups: TNewStaticText;
  TareasBackup: TArrayOfString;
  TareasBackupLeidas: Boolean;
  ConfigExistia: Boolean;

{ ---------- Utilidades ---------- }

function EjecutarOculto(const Archivo, Parametros: String): Integer;
var
  Codigo: Integer;
begin
  if Exec(Archivo, Parametros, '', SW_HIDE, ewWaitUntilTerminated, Codigo) then
    Result := Codigo
  else
    Result := -1;
  Log('Exec ' + Archivo + ' ' + Parametros + ' -> ' + IntToStr(Result));
end;

function Sistema(const Archivo: String): String;
begin
  Result := ExpandConstant('{sys}\') + Archivo;
end;

{ Ejecuta un .ps1 oculto. El script escribe su resultado en un archivo UTF-8
  que se devuelve linea por linea. }
function EjecutarScript(const Script, Argumentos: String; var Lineas: TArrayOfString): Integer;
var
  Salida: String;
begin
  Salida := ExpandConstant('{tmp}\superm-salida.txt');
  DeleteFile(Salida);
  Result := EjecutarOculto(Sistema('WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + Script + '" ' + Argumentos + ' -ArchivoSalida "' + Salida + '"');
  if not LoadStringsFromFile(Salida, Lineas) then
    SetArrayLength(Lineas, 0);
end;

function UnirLineas(const Lineas: TArrayOfString): String;
var
  I: Integer;
begin
  Result := '';
  for I := 0 to GetArrayLength(Lineas) - 1 do
    Result := Result + Lineas[I] + #13#10;
  Result := Trim(Result);
end;

function EscaparJson(Valor: String): String;
begin
  StringChangeEx(Valor, '\', '\\', True);
  StringChangeEx(Valor, '"', '\"', True);
  Result := Valor;
end;

function ExtraerEntre(const Texto, Inicio, Fin: String): String;
var
  P: Integer;
  Resto: String;
begin
  Result := '';
  P := Pos(Inicio, Texto);
  if P = 0 then Exit;
  Resto := Copy(Texto, P + Length(Inicio), MaxInt);
  P := Pos(Fin, Resto);
  if P = 0 then Exit;
  Result := Copy(Resto, 1, P - 1);
end;

function HoraValida(const Hora: String): Boolean;
var
  HH, MM: Integer;
begin
  Result := (Length(Hora) = 5) and (Hora[3] = ':');
  if not Result then Exit;
  HH := StrToIntDef(Copy(Hora, 1, 2), -1);
  MM := StrToIntDef(Copy(Hora, 4, 2), -1);
  Result := (HH >= 0) and (HH <= 23) and (MM >= 0) and (MM <= 59);
end;

function MinutosDelDia(const Hora: String): Integer;
begin
  Result := StrToIntDef(Copy(Hora, 1, 2), 0) * 60 + StrToIntDef(Copy(Hora, 4, 2), 0);
end;

{ ---------- Valores de configuracion ---------- }

function Servidor: String;
begin
  Result := Trim(PaginaBD.Values[0]);
  { Una barra final romperia las comillas al pasarlo por linea de comandos. }
  while (Length(Result) > 0) and (Result[Length(Result)] = '\') do
    Delete(Result, Length(Result), 1);
end;

function BaseDatos: String;
begin
  Result := Trim(PaginaBD.Values[1]);
end;

function Puerto: String;
begin
  Result := Trim(PaginaCaja.Values[0]);
end;

function NombreNegocio: String;
begin
  Result := Trim(PaginaCaja.Values[1]);
end;

function NombreImpresora: String;
begin
  Result := Trim(PaginaCaja.Values[2]);
end;

function HoraBackup: String;
begin
  Result := Trim(PaginaBackup.Values[0]);
end;

function UrlApp(Param: String): String;
begin
  Result := 'http://localhost:' + Puerto;
end;

function RutaConfig: String;
begin
  Result := AddBackslash(WizardDirValue) + ArchivoConfig;
end;

{ C:\SuperM\SuperMWeb -> C:\SuperM\backups, como en despliegue.md. }
function DirBackups: String;
var
  Padre: String;
begin
  Padre := ExtractFileDir(ExpandConstant('{app}'));
  if Length(Padre) <= 3 then
    Result := ExpandConstant('{app}\backups')
  else
    Result := AddBackslash(Padre) + 'backups';
end;

{ En una actualizacion se conserva appsettings.Production.json; de ahi se leen
  los valores que necesitan las tareas y los accesos directos. }
procedure CargarConfigExistente;
var
  Lineas: TArrayOfString;
  Texto, Valor: String;
  I: Integer;
begin
  if not LoadStringsFromFile(RutaConfig, Lineas) then Exit;
  Texto := '';
  for I := 0 to GetArrayLength(Lineas) - 1 do
    Texto := Texto + Lineas[I] + #10;

  Valor := ExtraerEntre(Texto, 'Data Source=', ';');
  StringChangeEx(Valor, '\\', '\', True);
  if Valor <> '' then PaginaBD.Values[0] := Valor;

  Valor := ExtraerEntre(Texto, 'Initial Catalog=', ';');
  if Valor <> '' then PaginaBD.Values[1] := Valor;

  Valor := ExtraerEntre(Texto, 'localhost:', '"');
  if Valor <> '' then PaginaCaja.Values[0] := Valor;
end;

{ ---------- Chrome ---------- }

function BuscarChrome: String;
var
  S: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe', '', S) or
     RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe', '', S) or
     RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe', '', S) then
  begin
    S := RemoveQuotes(S);
    if FileExists(S) then
    begin
      Result := S;
      Exit;
    end;
  end;

  S := ExpandConstant('{commonpf64}\Google\Chrome\Application\chrome.exe');
  if FileExists(S) then begin Result := S; Exit; end;
  S := ExpandConstant('{commonpf32}\Google\Chrome\Application\chrome.exe');
  if FileExists(S) then Result := S;
end;

function ChromeEncontrado: Boolean;
begin
  Result := BuscarChrome <> '';
end;

function RutaChrome(Param: String): String;
begin
  Result := BuscarChrome;
end;

{ ---------- Base de datos ---------- }

function EjecutarBD(const Accion: String; var Mensaje: String): Boolean;
var
  Lineas: TArrayOfString;
  Codigo: Integer;
begin
  ExtractTemporaryFile('superm-bd.ps1');
  Codigo := EjecutarScript(ExpandConstant('{tmp}\superm-bd.ps1'),
    '-Accion ' + Accion + ' -Servidor "' + Servidor + '" -BaseDatos "' + BaseDatos + '"', Lineas);
  Mensaje := UnirLineas(Lineas);
  if Mensaje = '' then
    Mensaje := 'PowerShell terminó con código ' + IntToStr(Codigo) + '.';
  Log('superm-bd.ps1 ' + Accion + ': ' + Mensaje);
  Result := Codigo = 0;
end;

procedure BotonProbarClick(Sender: TObject);
var
  Mensaje: String;
begin
  WizardForm.NextButton.Enabled := False;
  try
    if EjecutarBD('Probar', Mensaje) then
      MsgBox(Mensaje, mbInformation, MB_OK)
    else
      MsgBox('No se pudo validar la base de datos:' + #13#10#13#10 + Mensaje, mbError, MB_OK);
  finally
    WizardForm.NextButton.Enabled := True;
  end;
end;

{ ---------- Backups ya programados ---------- }

{ Tareas de backup que ya existen en el equipo (por ejemplo la del escritorio),
  como lineas "Nombre|HH:mm". Se leen una sola vez. }
procedure LeerTareasBackup;
begin
  if TareasBackupLeidas then Exit;
  TareasBackupLeidas := True;
  ExtractTemporaryFile('superm-bd.ps1');
  if EjecutarScript(ExpandConstant('{tmp}\superm-bd.ps1'),
       '-Accion TareasBackup -ExcluirTarea "' + NombreTareaBackup + '"', TareasBackup) <> 0 then
    SetArrayLength(TareasBackup, 0);
end;

{ Backups existentes a menos de una hora de la hora indicada. }
function TareasCercanas(const Hora: String): String;
var
  I, P, Diferencia: Integer;
  HoraTarea: String;
begin
  Result := '';
  LeerTareasBackup;
  for I := 0 to GetArrayLength(TareasBackup) - 1 do
  begin
    P := Pos('|', TareasBackup[I]);
    if P = 0 then Continue;
    HoraTarea := Copy(TareasBackup[I], P + 1, 5);
    if not HoraValida(HoraTarea) then Continue;
    { Abs devuelve Extended en Pascal Script: el valor absoluto va a mano. }
    Diferencia := MinutosDelDia(Hora) - MinutosDelDia(HoraTarea);
    if Diferencia < 0 then Diferencia := -Diferencia;
    if Diferencia > 720 then Diferencia := 1440 - Diferencia;
    if Diferencia < 60 then
      Result := Result + '  - ' + Copy(TareasBackup[I], 1, P - 1) + ' a las ' + HoraTarea + #13#10;
  end;
  Result := Trim(Result);
end;

procedure MostrarTareasBackup;
var
  I, P: Integer;
  Texto: String;
begin
  LeerTareasBackup;
  Texto := '';
  for I := 0 to GetArrayLength(TareasBackup) - 1 do
  begin
    P := Pos('|', TareasBackup[I]);
    if P > 0 then
      Texto := Texto + '  - ' + Copy(TareasBackup[I], 1, P - 1) + ' a las ' + Copy(TareasBackup[I], P + 1, 5) + #13#10;
  end;

  if Texto = '' then
    EtiquetaBackups.Caption := 'No se encontraron otras tareas de backup programadas en este equipo.'
  else
    EtiquetaBackups.Caption := 'Backups ya programados en este equipo (no se modifican):' + #13#10 + Texto + #13#10 +
      'Elija una hora distinta: dos backups de la misma base al mismo tiempo hacen fallar uno de ellos.';
end;

{ ---------- Servicio de Windows ---------- }

function ServicioExiste: Boolean;
begin
  Result := EjecutarOculto(Sistema('sc.exe'), 'query ' + NombreServicio) = 0;
end;

procedure DetenerServicio;
begin
  { net stop espera a que el SCM marque el servicio como detenido. Si quedo
    colgado, se fuerza el cierre para poder reemplazar los binarios. }
  EjecutarOculto(Sistema('net.exe'), 'stop ' + NombreServicio);
  EjecutarOculto(Sistema('taskkill.exe'), '/F /IM {#ExeName}');
  Sleep(1500);
end;

function AplicacionResponde(const Url: String): Boolean;
var
  Peticion: Variant;
begin
  Result := False;
  try
    Peticion := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Peticion.SetTimeouts(2000, 2000, 2000, 3000);
    Peticion.Open('GET', Url, False);
    Peticion.Send('');
    Result := Peticion.Status = 200;
  except
    Result := False;
  end;
end;

procedure InstalarServicio;
var
  Sc, RutaBinario, Accion: String;
  I, Codigo: Integer;
  Responde: Boolean;
begin
  Sc := Sistema('sc.exe');
  RutaBinario := '"\"' + ExpandConstant('{app}\{#ExeName}') + '\""';

  if ServicioExiste then Accion := 'config' else Accion := 'create';
  Codigo := EjecutarOculto(Sc, Accion + ' ' + NombreServicio + ' binPath= ' + RutaBinario +
                               ' start= auto DisplayName= "{#AppName}"');
  if Codigo <> 0 then
  begin
    SuppressibleMsgBox('No se pudo registrar el servicio ' + NombreServicio +
      ' (sc.exe devolvió ' + IntToStr(Codigo) + '). Revise el registro de instalación.',
      mbError, MB_OK, IDOK);
    Exit;
  end;

  EjecutarOculto(Sc, 'description ' + NombreServicio + ' "Punto de venta SuperM sobre Kestrel en ' + UrlApp('') + '"');
  { Reinicio automatico a los 5 s en los tres primeros fallos; el contador se
    reinicia tras un dia. Igual que scripts/install-service.ps1. }
  EjecutarOculto(Sc, 'failure ' + NombreServicio + ' reset= 86400 actions= restart/5000/restart/5000/restart/5000');
  EjecutarOculto(Sistema('net.exe'), 'start ' + NombreServicio);

  WizardForm.StatusLabel.Caption := 'Esperando a que la aplicación responda en ' + UrlApp('') + '...';
  Responde := False;
  for I := 1 to 30 do
  begin
    if AplicacionResponde(UrlApp('') + '/health') then
    begin
      Responde := True;
      Break;
    end;
    Sleep(1000);
  end;

  if not Responde then
    SuppressibleMsgBox('El servicio ' + NombreServicio + ' quedó instalado, pero la aplicación no respondió en ' +
      UrlApp('') + '/health.' + #13#10#13#10 +
      'Ese chequeo incluye la conexión a SQL Server: si la cuenta del servicio no tiene acceso a la base, falla aquí.' + #13#10 +
      'Revise los registros en ' + ExpandConstant('{app}\logs') + ' y la configuración en ' +
      ExpandConstant('{app}\') + ArchivoConfig + '.', mbError, MB_OK, IDOK);
end;

{ ---------- Tareas opcionales ---------- }

procedure EscribirConfiguracion;
var
  L: TArrayOfString;
  Cadena: String;
begin
  Cadena := 'Data Source=' + Servidor + ';Initial Catalog=' + BaseDatos +
            ';Integrated Security=True;TrustServerCertificate=True';

  SetArrayLength(L, 17);
  L[0]  := '// Generado por el instalador de {#AppName}. Solo se crea en la primera';
  L[1]  := '// instalacion; las actualizaciones lo conservan. Sus valores reemplazan a';
  L[2]  := '// los de appsettings.json. Tras editarlo: Restart-Service ' + NombreServicio;
  L[3]  := '{';
  L[4]  := '  "Urls": "' + UrlApp('') + '",';
  L[5]  := '  "ConnectionStrings": {';
  L[6]  := '    "SuperMDB": "' + EscaparJson(Cadena) + '"';
  L[7]  := '  },';
  L[8]  := '  "Printer": {';
  L[9]  := '    "Tipo": "Local",';
  L[10] := '    "NombreParcial": "' + EscaparJson(NombreImpresora) + '"';
  L[11] := '  },';
  L[12] := '  "Negocio": {';
  L[13] := '    "Nombre": "' + EscaparJson(NombreNegocio) + '"';
  L[14] := '  }';
  L[15] := '}';
  L[16] := '';

  if not SaveStringsToUTF8File(ExpandConstant('{app}\') + ArchivoConfig, L, False) then
    SuppressibleMsgBox('No se pudo escribir ' + ArchivoConfig + '.', mbError, MB_OK, IDOK);
end;

procedure ProgramarBackup;
var
  Lineas: TArrayOfString;
  Codigo: Integer;
  Mensaje: String;
begin
  Codigo := EjecutarScript(ExpandConstant('{app}\scripts\programar-backup.ps1'),
    '-Hora ' + HoraBackup + ' -Server "' + Servidor + '" -Database "' + BaseDatos + '"' +
    ' -BackupDir "' + DirBackups + '" -NombreTarea "' + NombreTareaBackup + '"', Lineas);
  Mensaje := UnirLineas(Lineas);
  Log('programar-backup.ps1: ' + Mensaje);
  if Codigo <> 0 then
    SuppressibleMsgBox('No se programó el backup diario adicional:' + #13#10#13#10 + Mensaje, mbError, MB_OK, IDOK);
end;

procedure ConfigurarEnergia;
begin
  EjecutarOculto(Sistema('powercfg.exe'), '/change standby-timeout-ac 0');
  EjecutarOculto(Sistema('powercfg.exe'), '/change hibernate-timeout-ac 0');
  EjecutarOculto(Sistema('powercfg.exe'), '/change monitor-timeout-ac 0');
end;

{ ---------- Asistente ---------- }

procedure InitializeWizard;
var
  Boton: TNewButton;
begin
  PaginaBD := CreateInputQueryPage(wpSelectDir,
    'Base de datos',
    'Servidor SQL Server y base que usa la caja.',
    'La aplicación se conecta con la seguridad integrada de Windows, sin usuario ni contraseña. ' +
    'Es la misma base que usa el sistema de escritorio. Use "Probar conexión" antes de continuar.');
  PaginaBD.Add('Servidor SQL Server (ej. localhost o localhost\SQLEXPRESS):', False);
  PaginaBD.Add('Base de datos:', False);
  PaginaBD.Values[0] := 'localhost';
  PaginaBD.Values[1] := 'SuperM';

  Boton := TNewButton.Create(PaginaBD);
  Boton.Parent := PaginaBD.Surface;
  Boton.Left := PaginaBD.Edits[1].Left;
  Boton.Top := PaginaBD.Edits[1].Top + PaginaBD.Edits[1].Height + ScaleY(16);
  Boton.Width := ScaleX(160);
  Boton.Height := WizardForm.NextButton.Height;
  Boton.Caption := 'Probar conexión';
  Boton.OnClick := @BotonProbarClick;

  PaginaCaja := CreateInputQueryPage(PaginaBD.ID,
    'Datos de la caja',
    'Puerto, factura e impresora.',
    'Estos valores se guardan en ' + ArchivoConfig + ' dentro de la carpeta de instalación.');
  PaginaCaja.Add('Puerto de la aplicación (http://localhost:PUERTO):', False);
  PaginaCaja.Add('Nombre del negocio (sale en la factura):', False);
  PaginaCaja.Add('Parte del nombre de la impresora POS en Windows:', False);
  PaginaCaja.Values[0] := '5000';
  PaginaCaja.Values[1] := 'Minimarket el Cairo';
  PaginaCaja.Values[2] := 'POS';

  PaginaBackup := CreateInputQueryPage(wpSelectTasks,
    'Backup diario adicional',
    'Hora del backup de la base.',
    'Corre como SYSTEM, sin necesidad de sesión abierta; si el equipo está apagado a esa hora, ' +
    'se hace apenas encienda. Guarda 30 días de archivos y usa COPY_ONLY, así que no altera otros backups.');
  PaginaBackup.Add('Hora (formato 24 horas, HH:MM):', False);
  PaginaBackup.Values[0] := GetPreviousData('HoraBackup', HoraBackupPorDefecto);

  EtiquetaBackups := TNewStaticText.Create(PaginaBackup);
  EtiquetaBackups.Parent := PaginaBackup.Surface;
  EtiquetaBackups.AutoSize := False;
  EtiquetaBackups.WordWrap := True;
  EtiquetaBackups.Left := 0;
  EtiquetaBackups.Top := PaginaBackup.Edits[0].Top + PaginaBackup.Edits[0].Height + ScaleY(16);
  EtiquetaBackups.Width := PaginaBackup.SurfaceWidth;
  EtiquetaBackups.Height := ScaleY(120);
  EtiquetaBackups.Caption := '';
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'HoraBackup', HoraBackup);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (PageID = PaginaBD.ID) or (PageID = PaginaCaja.ID) then
  begin
    if FileExists(RutaConfig) then
    begin
      CargarConfigExistente;
      Result := True;
    end;
  end
  else if PageID = PaginaBackup.ID then
    Result := not WizardIsTaskSelected('backup');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PaginaBackup.ID then
    MostrarTareasBackup;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Mensaje, Cercanas: String;
  N: Integer;
begin
  Result := True;

  if CurPageID = PaginaBD.ID then
  begin
    if (Servidor = '') or (BaseDatos = '') then
    begin
      MsgBox('Indique el servidor y la base de datos.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if not EjecutarBD('Probar', Mensaje) then
      Result := MsgBox('No se pudo validar la base de datos:' + #13#10#13#10 + Mensaje + #13#10#13#10 +
                       '¿Desea continuar de todos modos?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end
  else if CurPageID = PaginaCaja.ID then
  begin
    N := StrToIntDef(Puerto, 0);
    if (N < 1) or (N > 65535) then
    begin
      MsgBox('El puerto debe ser un número entre 1 y 65535.', mbError, MB_OK);
      Result := False;
    end
    else if NombreNegocio = '' then
    begin
      MsgBox('Indique el nombre del negocio.', mbError, MB_OK);
      Result := False;
    end
    else if NombreImpresora = '' then
    begin
      MsgBox('Indique parte del nombre de la impresora (por ejemplo POS).', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = PaginaBackup.ID then
  begin
    if not HoraValida(HoraBackup) then
    begin
      MsgBox('Escriba la hora en formato 24 horas HH:MM, por ejemplo 03:00 o 13:30.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    Cercanas := TareasCercanas(HoraBackup);
    if Cercanas <> '' then
      Result := MsgBox('La hora elegida queda a menos de una hora de este backup existente:' + #13#10#13#10 +
                       Cercanas + #13#10#13#10 +
                       'Si coinciden, uno de los dos falla. ¿Usar esta hora de todos modos?',
                       mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine + 'Configuración:' + NewLine;
  if FileExists(RutaConfig) then
    Result := Result + Space + 'Se conserva ' + ArchivoConfig + ' existente' + NewLine;
  Result := Result +
    Space + 'Servidor SQL: ' + Servidor + NewLine +
    Space + 'Base de datos: ' + BaseDatos + NewLine +
    Space + 'Dirección: ' + UrlApp('');
  if not FileExists(RutaConfig) then
    Result := Result + NewLine +
      Space + 'Negocio: ' + NombreNegocio + NewLine +
      Space + 'Impresora: ' + NombreImpresora;
  if MemoTasksInfo <> '' then
    Result := Result + NewLine + NewLine + MemoTasksInfo;
  if WizardIsTaskSelected('backup') then
    Result := Result + NewLine + NewLine + 'Backup diario adicional:' + NewLine +
      Space + 'Hora: ' + HoraBackup + ' (o al encender el equipo si estaba apagado)' + NewLine +
      Space + 'Carpeta: ' + DirBackups;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  ConfigExistia := FileExists(RutaConfig);
  if ConfigExistia then CargarConfigExistente;
  if ServicioExiste then DetenerServicio;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Mensaje: String;
begin
  if CurStep <> ssPostInstall then Exit;

  if not ConfigExistia then
    EscribirConfiguracion;

  if WizardIsTaskSelected('permisosbd') then
  begin
    WizardForm.StatusLabel.Caption := 'Dando acceso a la base de datos a la cuenta del servicio...';
    if not EjecutarBD('Permisos', Mensaje) then
      SuppressibleMsgBox('No se pudieron otorgar los permisos en la base de datos:' + #13#10#13#10 + Mensaje + #13#10#13#10 +
        'Ejecute el instalador con un usuario administrador de SQL Server, ' +
        'o dé los permisos a mano (ver despliegue.md, sección 7).', mbError, MB_OK, IDOK);
  end;

  if WizardIsTaskSelected('backup') then
  begin
    WizardForm.StatusLabel.Caption := 'Programando el backup diario adicional...';
    ProgramarBackup;
  end;

  if WizardIsTaskSelected('energia') then
    ConfigurarEnergia;

  WizardForm.StatusLabel.Caption := 'Registrando e iniciando el servicio ' + NombreServicio + '...';
  InstalarServicio;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usUninstall then Exit;

  { Antes de borrar archivos: el servicio tiene abierto el ejecutable. Solo se
    borra la tarea de backup propia, nunca otras (como la del escritorio). }
  EjecutarOculto(Sistema('net.exe'), 'stop ' + NombreServicio);
  EjecutarOculto(Sistema('taskkill.exe'), '/F /IM {#ExeName}');
  EjecutarOculto(Sistema('sc.exe'), 'delete ' + NombreServicio);
  EjecutarOculto(Sistema('schtasks.exe'), '/Delete /F /TN "' + NombreTareaBackup + '"');
  Sleep(1500);
end;
