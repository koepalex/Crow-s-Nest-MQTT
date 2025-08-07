dotnet test --collect:"XPlat Code Coverage;Format=json,cobertura" /p:CoverletOutput=./TestResults/coverage/   
reportgenerator -reports:"./tests/**/coverage.cobertura.xml" -targetdir:"./TestResults/CoverageReport" -reporttypes:"Html;Cobertura;JsonSummary"
start .\TestResults\CoverageReport\index.htm