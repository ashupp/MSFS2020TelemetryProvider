# MSFS2020TelemetryProvider
Microsoft Flight Simulator 2020 Telemetry Provider for SimFeedback / SFX-100  

Please support this great project.  
https://opensfx.com

## Please read the installation instructions below BEFORE download 

## Warning  
**Unpredicted movement of you SFX-100 could happen at any time when this plugin is enabled.**  
I am not responsible of any damages caused by usage of this plugin!  
**You have been warned!**  

## Installation
- Do not click on Download on this page. Download the file from the release tab or use the following link:
  - [latest MSFS2020SimFeedbackTelemetryProviderSetup.exe](https://github.com/ashupp/MSFS2020TelemetryProvider/releases/latest/download/MSFS2020SimFeedbackTelemetryProviderSetup.exe)    
- Download and run installer
- **Please select Simfeedback root folder as desired installation path**

## Configuration / Stuttering of motion
First make sure you are running the newest version of MSFS2020.  
If you are encountering stuttering of the rig or of the sim you can try to reduce the TelemetryUpdateFrequency
in the configuration file next to the telemetryprovider.dll.  
"MSFS2020TelemetryProvider.dll.config".
Try a setting of 60 and try to set AutoCalculateRateLimiter to "False".
