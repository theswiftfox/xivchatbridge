

# XIVChatBridge

FFXIV ChatBridge plugin for Dalamud. Allowing to access the chat from the browser.

## How to access from other devices  
⚠️ Only do this if you know what you are doing. There is currently no authentication enabled.  

### Setup Network & Firewall rules  

First, add a new rule to allow the plugin to listen on all interfaces:  
Run this in an powershell as admin:  
```powershell
netsh http add urlacl url="http://+:9876/" user=everyone
```
Now add a new inbound firewall rule allowing connections on port `9876` on all networks.  
After you have done this enable the non local access in the plugin settings and reload.  
Replace the port with your custom one if you changed it, of course.