$env:ASPNETCORE_ENVIRONMENT='Development'
$log = 'D:\Projects\gitlab\transfer123\api\Transfers\backend\src\Api\api-stdout.log'
$err = 'D:\Projects\gitlab\transfer123\api\Transfers\backend\src\Api\api-stderr.log'
Remove-Item -Force $log, $err -ErrorAction SilentlyContinue
$proc = Start-Process -NoNewWindow -PassThru -RedirectStandardOutput $log -RedirectStandardError $err -WorkingDirectory 'D:\Projects\gitlab\transfer123\api\Transfers\backend\src\Api' -FilePath 'dotnet' -ArgumentList 'run --no-build'
Write-Host "API PID: $($proc.Id)"
$proc.Id | Out-File 'D:\Projects\gitlab\transfer123\api\Transfers\backend\src\Api\api.pid'
