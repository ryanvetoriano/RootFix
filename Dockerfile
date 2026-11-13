# Imagem da API .NET para o Render (Web Service com runtime Docker).
# O Render injeta PORT; o Program.cs escuta nela. Segredos entram como variaveis de ambiente
# no painel do Render, nunca na imagem.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaura primeiro so com os .csproj: a camada fica em cache enquanto as dependencias nao mudam.
COPY RootFix.sln ./
COPY RootFix.Domain/RootFix.Domain.csproj RootFix.Domain/
COPY RootFix.Application/RootFix.Application.csproj RootFix.Application/
COPY RootFix.Infrastructure/RootFix.Infrastructure.csproj RootFix.Infrastructure/
COPY RootFix.API/RootFix.API.csproj RootFix.API/
RUN dotnet restore RootFix.API/RootFix.API.csproj -r linux-x64 -p:PublishReadyToRun=true

COPY RootFix.Domain/ RootFix.Domain/
COPY RootFix.Application/ RootFix.Application/
COPY RootFix.Infrastructure/ RootFix.Infrastructure/
COPY RootFix.API/ RootFix.API/
# ReadyToRun: o código já sai pré-compilado para linux-x64. Com 0,1 CPU no plano gratuito, a
# compilação JIT na partida custava mais de um minuto a cada vez que o serviço acordava.
RUN dotnet publish RootFix.API/RootFix.API.csproj -c Release -o /app/publish --no-restore     -r linux-x64 --self-contained false -p:PublishReadyToRun=true -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Plano gratuito do Render: 512 MB de RAM e 0,1 CPU.
#  - DOTNET_gcServer=0: GC de estação de trabalho, que reserva bem menos memória que o de servidor.
#  - DOTNET_GCHeapHardLimitPercent=0x46 (70%): o heap não passa de 70% do limite do container,
#    sobrando memória para o runtime e o driver do Oracle.
#  - TZ: só para os horários dos logs; as datas gravadas já seguem o RelogioDoRootFix.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimitPercent=0x46 \
    TZ=America/Sao_Paulo
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "RootFix.API.dll"]
