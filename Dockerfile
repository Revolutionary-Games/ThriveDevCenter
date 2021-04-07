FROM fedora:33 as builder
ENV DOTNET_VERSION "5.0"

RUN dnf install -y --setopt=deltarpm=false dotnet-sdk-${DOTNET_VERSION} && dnf clean all

COPY ThriveDevCenter.sln /root/build/
# Causes a bunch of extra layers because docker folder copy is terrible
COPY Client/ /root/build/Client
COPY Server/ /root/build/Server
COPY Shared/ /root/build/Shared

WORKDIR /root/build

# Building release binaries
RUN dotnet publish -c Release Client/ThriveDevCenter.Client.csproj && \
    dotnet publish -c Release Server/ThriveDevCenter.Server.csproj

# Migrations file
RUN dotnet tool install --global dotnet-ef
RUN PATH="$PATH:/root/.dotnet/tools" dotnet ef migrations script --idempotent \
    --project Server/ThriveDevCenter.Server.csproj -o /migration.sql

FROM fedora:33 as proxy
ENV DOTNET_VERSION "5.0"

RUN dnf install -y --setopt=deltarpm=false nginx && dnf clean all

COPY --from=builder /root/build/Client/bin/Release/net${DOTNET_VERSION}/publish/wwwroot/ \
    /var/www/html/

COPY docker_nginx.conf /etc/nginx/nginx.conf

RUN ln -sf /dev/stdout /var/log/nginx/access.log && ln -sf /dev/stderr /var/log/nginx/error.log

CMD nginx

FROM fedora:33 as application
ENV DOTNET_VERSION "5.0"

RUN dnf install -y --setopt=deltarpm=false dotnet-runtime-${DOTNET_VERSION} && dnf clean all

RUN useradd thrivedevcenter -m

COPY --from=builder /root/build/Server/bin/Release/net${DOTNET_VERSION}/publish/ \
    /home/thrivedevcenter

WORKDIR /home/thrivedevcenter
CMD /home/thrivedevcenter/ThriveDevCenter.Server