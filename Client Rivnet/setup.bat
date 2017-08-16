cd %~dp0

Schtasks.exe /Create /XML "task.xml" /tn Rivnet

mkdir "C:/Program Files (x86)/Rivnet"
copy "./ConfigParser.dll" "C:/Program Files (x86)/Rivnet"
copy "./rivnet.conf" "C:/Program Files (x86)/Rivnet"
copy "./Rivnet.exe" "C:/Program Files (x86)/Rivnet"
copy "./rivnet.ico" "C:/Program Files (x86)/Rivnet"
copy "./uninstall.bat" "C:/Program Files (x86)/Rivnet"