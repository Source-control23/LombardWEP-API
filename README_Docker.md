LombardAPI_NEW - Docker packaging and deployment

This README explains how to build, run, and push the Docker image for LombardAPI_NEW.

Important summary
- The application uses Active Directory PowerShell module (Import-Module ActiveDirectory) inside `Shared/Powershell.cs`. This requires Windows containers with the RSAT/AD PowerShell module installed. Linux containers will not support those features.
- Do not bake secrets into the image. Use environment variables, Docker secrets, or cloud secret manager.

Files added
- `Dockerfile` - multi-stage Windows container image for .NET8
- `.dockerignore` - recommended ignore list

Prerequisites
- Docker Desktop on Windows with "Use Windows containers" selected, or a Windows Server host with Docker Engine.
- Docker20.x+
- Network access to PostgreSQL and SMTP from the container.
- Ensure the AD PowerShell module (RSAT) is available inside the container image or installed on the host.

Build (example)
1. Open PowerShell or CMD in repository root (where `Dockerfile` is located)
2. Build the image:
 docker build -t lombardapi:1.0 .

Run (example)
- Run container and provide required environment variables (example):
 docker run -d --name lombardapi \
 -p5000:80 \
 -e "ConnectionStrings__DefaultConnection=Host=host:5432;Database=yourdb;Username=user;Password=pass" \
 -e "EmailSettings__FromEmail=from@example.com" \
 -e "EmailSettings__ToEmail=to@example.com" \
 -e "EmailSettings__SmtpHost=smtp.example.com" \
 -e "EmailSettings__SmtpPort=25" \
 lombardapi:1.0

Test
- Open Swagger: http://localhost:5000/swagger/index.html
- Example API: GET http://localhost:5000/api/account-unlock/ILOM35124

Environment variable naming
- Use double underscore `__` to represent nested config keys, e.g., `EmailSettings__SmtpHost`.

Push to registry
- Tag and push:
 docker tag lombardapi:1.0 yourrepo/lombardapi:1.0
 docker push yourrepo/lombardapi:1.0

Notes and caveats
- Active Directory: The base runtime image may not include RSAT AD module. To run GAL or AccountUnlock features, install RSAT into the image or run the container on a host with the module.
- HTTPS: Image exposes port80. For production enable HTTPS termination at reverse proxy or bind certificate and enable `UseHttpsRedirection`.
- Secrets: use Docker secrets / orchestrator secret store for DB and SMTP credentials.

If needed, I can:
- Modify the Dockerfile to install the AD PowerShell module into the image (requires additional steps and an internet connection during build).
- Provide a Linux refactor plan to avoid Windows-only PowerShell AD dependency.
