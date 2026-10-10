; HWiNFO "Reporting to Gadget" registry indices (free HWiNFO — no shared memory / plugin).
; Setup: https://docs.rainmeter.net/tips/hwinfo/
; 1) HWiNFO Settings: leave Shared Memory OFF.
; 2) Sensors → Configure Sensors → HWiNFO Gadget tab → Enable reporting to Gadget.
; 3) For each reading, check "Report value in Gadget" and note the Index number.
; 4) List indices: reg query HKCU\SOFTWARE\HWiNFO64\VSB  (or HKLM if HWiNFO runs elevated)
; Portable HWiNFO: C:\Users\Augus\Desktop Folders\AcerPH18\hwi_854
;
; If indices change after adding/removing gadget sensors, update the four Index* values below.

[Variables]
RegHKey=HKEY_CURRENT_USER
; Use HKEY_LOCAL_MACHINE if HWiNFO runs as admin and HKCU has no VSB values.

IndexCpuTemp=0
IndexCpuPower=1
IndexGpuTemp=2
IndexFanRpm=3
