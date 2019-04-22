@echo off
netstat -a -n | find ":3392" >NUL && start %windir%\system32\mstsc.exe && exit
start plink -L 3392:192.168.2.38:3389 -i "C:\Users\white\Desktop\new-PuTTY\priv-key.p
pk" -l protssh bluehive.circ.rochester.edu -N -hide_console -use_vintela_gui_w_pwd
:wait_for_ssh
netstat -a -n | find ":3392" >NUL && start %windir%\system32\mstsc.exe && exit
goto :wait_for_ssh
exit