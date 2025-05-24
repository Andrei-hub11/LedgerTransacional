# Deploy-LedgerTransacional.ps1
# Script para deploy inicial do Ledger Transacional

param(
    [string]$StackName = "ledger-transacional",
    [string]$S3BucketName = "",  # Bucket para armazenar o código das funções Lambda
    [string]$Region = "us-east-1",
    [string]$SamTemplatePath = ".\infrastructure\templates\sam-template.yaml",
    [string]$AccountsTableName = "Accounts",
    [string]$TransactionsTableName = "Transactions",
    [string]$EntriesTableName = "Entries",
    [string]$QueueName = "TransactionQueue"
)

try {
    $awsVersion = aws --version
    Write-Host "AWS CLI encontrado: $awsVersion" -ForegroundColor Green
}
catch {
    Write-Host "AWS CLI não encontrado. Por favor, instale o AWS CLI e configure suas credenciais." -ForegroundColor Red
    exit 1
}

try {
    $samVersion = sam --version
    Write-Host "AWS SAM CLI encontrado: $samVersion" -ForegroundColor Green
}
catch {
    Write-Host "AWS SAM CLI não encontrado. Por favor, instale o AWS SAM CLI: https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/serverless-sam-cli-install.html" -ForegroundColor Red
    exit 1
}

try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK encontrado: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host ".NET SDK não encontrado. Por favor, instale o .NET SDK: https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

function Ensure-S3Bucket {
    param (
        [string]$BucketName
    )

    if ([string]::IsNullOrEmpty($BucketName)) {
        $accountId = (aws sts get-caller-identity --query "Account" --output text)
        $BucketName = "ledger-transacional-$accountId-$Region"
    }

    $bucketExists = $false
    try {
        $bucketCheck = aws s3api head-bucket --bucket $BucketName 2>&1
        $bucketExists = $true
        Write-Host "Bucket $BucketName já existe." -ForegroundColor Green
    }
    catch {
        Write-Host "Bucket $BucketName não existe, criando..." -ForegroundColor Yellow
        aws s3 mb "s3://$BucketName" --region $Region
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Erro ao criar o bucket S3. Verifique se o nome é único e tente novamente." -ForegroundColor Red
            exit 1
        }
        
        Write-Host "Bucket $BucketName criado com sucesso." -ForegroundColor Green
    }

    return $BucketName
}

function Create-DynamoDBTables {
    Write-Host "Criando tabela $AccountsTableName..." -ForegroundColor Yellow
    $accountsTableExists = $false
    
    try {
        $tableInfo = aws dynamodb describe-table --table-name $AccountsTableName 2>&1
        $accountsTableExists = $true
        Write-Host "Tabela $AccountsTableName já existe." -ForegroundColor Green
    }
    catch {
        aws dynamodb create-table `
            --table-name $AccountsTableName `
            --attribute-definitions AttributeName=AccountId,AttributeType=S `
            --key-schema AttributeName=AccountId,KeyType=HASH `
            --billing-mode PAY_PER_REQUEST `
            --region $Region

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Erro ao criar a tabela $AccountsTableName." -ForegroundColor Red
            exit 1
        }

        Write-Host "Aguardando a criação da tabela $AccountsTableName..." -ForegroundColor Yellow
        aws dynamodb wait table-exists --table-name $AccountsTableName --region $Region
        Write-Host "Tabela $AccountsTableName criada com sucesso." -ForegroundColor Green
    }

    Write-Host "Criando tabela $TransactionsTableName..." -ForegroundColor Yellow
    $transactionsTableExists = $false
    
    try {
        $tableInfo = aws dynamodb describe-table --table-name $TransactionsTableName 2>&1
        $transactionsTableExists = $true
        Write-Host "Tabela $TransactionsTableName já existe." -ForegroundColor Green
    }
    catch {
        aws dynamodb create-table `
            --table-name $TransactionsTableName `
            --attribute-definitions AttributeName=TransactionId,AttributeType=S `
            --key-schema AttributeName=TransactionId,KeyType=HASH `
            --billing-mode PAY_PER_REQUEST `
            --region $Region

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Erro ao criar a tabela $TransactionsTableName." -ForegroundColor Red
            exit 1
        }

        Write-Host "Aguardando a criação da tabela $TransactionsTableName..." -ForegroundColor Yellow
        aws dynamodb wait table-exists --table-name $TransactionsTableName --region $Region
        Write-Host "Tabela $TransactionsTableName criada com sucesso." -ForegroundColor Green
    }

    Write-Host "Criando tabela $EntriesTableName..." -ForegroundColor Yellow
    $entriesTableExists = $false
    
    try {
        $tableInfo = aws dynamodb describe-table --table-name $EntriesTableName 2>&1
        $entriesTableExists = $true
        Write-Host "Tabela $EntriesTableName já existe." -ForegroundColor Green
    }
    catch {
        aws dynamodb create-table `
            --table-name $EntriesTableName `
            --attribute-definitions `
                AttributeName=EntryId,AttributeType=S `
                AttributeName=TransactionId,AttributeType=S `
            --key-schema `
                AttributeName=EntryId,KeyType=HASH `
                AttributeName=TransactionId,KeyType=RANGE `
            --billing-mode PAY_PER_REQUEST `
            --region $Region

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Erro ao criar a tabela $EntriesTableName." -ForegroundColor Red
            exit 1
        }

        Write-Host "Aguardando a criação da tabela $EntriesTableName..." -ForegroundColor Yellow
        aws dynamodb wait table-exists --table-name $EntriesTableName --region $Region
        Write-Host "Tabela $EntriesTableName criada com sucesso." -ForegroundColor Green
    }
}

function Create-SQSQueue {
    param (
        [string]$QueueName
    )
    
    Write-Host "Verificando SQS Queue $QueueName..." -ForegroundColor Yellow
    
    try {
        $queueUrl = aws sqs get-queue-url --queue-name $QueueName --region $Region --query "QueueUrl" --output text 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "SQS Queue $QueueName já existe: $queueUrl" -ForegroundColor Green
            return $queueUrl
        }
    }
    catch {
        Write-Host "SQS Queue $QueueName não existe, criando..." -ForegroundColor Yellow
        
        $queueUrl = aws sqs create-queue --queue-name $QueueName --region $Region --attributes VisibilityTimeout=180,MessageRetentionPeriod=1209600 --query "QueueUrl" --output text
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Erro ao criar a SQS Queue $QueueName." -ForegroundColor Red
            exit 1
        }
        
        Write-Host "SQS Queue $QueueName criada com sucesso: $queueUrl" -ForegroundColor Green
        return $queueUrl
    }
}

function Build-Project {
    $projectPath = (Get-Location)
    Write-Host "Compilando projeto de funções Lambda..." -ForegroundColor Yellow
    
    $srcPath = Join-Path -Path $projectPath -ChildPath "src"
    if (-not (Test-Path $srcPath)) {
        Write-Host "Diretório 'src' não encontrado. Verifique se você está na raiz do projeto." -ForegroundColor Red
        exit 1
    }

    $functionsPath = Join-Path -Path $srcPath -ChildPath "LedgerTransacional.Functions"
    if (-not (Test-Path $functionsPath)) {
        Write-Host "Projeto 'LedgerTransacional.Functions' não encontrado em $functionsPath" -ForegroundColor Red
        exit 1
    }

    Write-Host "Restaurando dependências do projeto..." -ForegroundColor Yellow
    dotnet restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Erro ao restaurar as dependências do projeto." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Compilando a solução completa..." -ForegroundColor Yellow
    dotnet build -c Release
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Erro ao compilar o projeto. Verifique os erros acima." -ForegroundColor Red
        exit 1
    }
    
    $outputPath = Join-Path -Path $functionsPath -ChildPath "bin\Release\net8.0\LedgerTransacional.Functions.dll"
    if (-not (Test-Path $outputPath)) {
        Write-Host "O arquivo de saída não foi gerado em $outputPath" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Projeto compilado com sucesso." -ForegroundColor Green
}

function Deploy-SAM {
    param (
        [string]$S3BucketName
    )
    
    if (-not (Test-Path $SamTemplatePath)) {
        Write-Host "Template SAM não encontrado em: $SamTemplatePath" -ForegroundColor Red
        exit 1
    }

    Write-Host "Fazendo o build e deploy da aplicação SAM..." -ForegroundColor Yellow
    
    $functionsOutputPath = Join-Path -Path $PSScriptRoot -ChildPath "src\LedgerTransacional.Functions\bin\Release\net8.0\LedgerTransacional.Functions.dll"
    if (-not (Test-Path $functionsOutputPath)) {
        Write-Host "Arquivo de funções compilado não encontrado em: $functionsOutputPath" -ForegroundColor Red
        Write-Host "Certifique-se de que a compilação foi bem-sucedida antes de prosseguir com o SAM build." -ForegroundColor Red
        exit 1
    }
    
    sam build -t $SamTemplatePath --region $Region

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Erro ao fazer o build da aplicação SAM." -ForegroundColor Red
        exit 1
    }
    
    $samConfigExists = Test-Path -Path "samconfig.toml"
    
    if (-not $samConfigExists) {
        Write-Host "Nenhuma configuração SAM encontrada. Iniciando deploy guiado..." -ForegroundColor Yellow
        Write-Host "IMPORTANTE: Quando perguntado sobre 'Save arguments to configuration file', responda Y e aceite o nome padrão 'samconfig.toml'" -ForegroundColor Cyan
        Write-Host "IMPORTANTE: Quando perguntado sobre a região, especifique '$Region'" -ForegroundColor Cyan
        
        sam deploy --guided --stack-name $StackName `
            --parameter-overrides "AccountsTableName=$AccountsTableName TransactionsTableName=$TransactionsTableName EntriesTableName=$EntriesTableName" `
            --capabilities CAPABILITY_IAM `
            --region $Region
    }
    else {
        Write-Host "Configuração SAM encontrada. Utilizando configurações existentes..." -ForegroundColor Yellow
        sam deploy --stack-name $StackName `
            --parameter-overrides "AccountsTableName=$AccountsTableName TransactionsTableName=$TransactionsTableName EntriesTableName=$EntriesTableName" `
            --capabilities CAPABILITY_IAM `
            --region $Region
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Erro ao fazer o deploy da aplicação SAM." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Aplicação implantada com sucesso!" -ForegroundColor Green
}

function Show-ApiInformation {
    Write-Host "Obtendo informações sobre a API implantada..." -ForegroundColor Yellow
    
    $apiInfo = aws cloudformation describe-stacks --stack-name $StackName --region $Region --query "Stacks[0].Outputs[?OutputKey=='GetAccountsApi' || OutputKey=='CreateAccountApi' || OutputKey=='UpdateAccountApi' || OutputKey=='CreateTransactionApi' || OutputKey=='GetTransactionsApi' || OutputKey=='ReverseTransactionApi' || OutputKey=='TransactionQueueUrl' || OutputKey=='ProcessTransactionFunction']" --output json | ConvertFrom-Json
    
    if ($apiInfo) {
        Write-Host "`nEndpoints da API:" -ForegroundColor Cyan
        foreach ($output in $apiInfo) {
            Write-Host "$($output.OutputKey): $($output.OutputValue)" -ForegroundColor Green
        }
        
        Write-Host "`nExemplos de uso:" -ForegroundColor Cyan
        $getAccountsEndpoint = ($apiInfo | Where-Object { $_.OutputKey -eq "GetAccountsApi" }).OutputValue
        $createAccountEndpoint = ($apiInfo | Where-Object { $_.OutputKey -eq "CreateAccountApi" }).OutputValue
        $updateAccountEndpoint = ($apiInfo | Where-Object { $_.OutputKey -eq "UpdateAccountApi" }).OutputValue
        $createTransactionEndpoint = ($apiInfo | Where-Object { $_.OutputKey -eq "CreateTransactionApi" }).OutputValue
        $getTransactionsEndpoint = ($apiInfo | Where-Object { $_.OutputKey -eq "GetTransactionsApi" }).OutputValue
        $reverseTransactionEndpoint = ($apiInfo | Where-Object { $_.OutputKey -eq "ReverseTransactionApi" }).OutputValue
        $transactionQueueUrl = ($apiInfo | Where-Object { $_.OutputKey -eq "TransactionQueueUrl" }).OutputValue
        $processTransactionArn = ($apiInfo | Where-Object { $_.OutputKey -eq "ProcessTransactionFunction" }).OutputValue
        
        if ($getAccountsEndpoint) {
            Write-Host "Para listar todas as contas:" -ForegroundColor Yellow
            Write-Host "curl -X GET $getAccountsEndpoint" -ForegroundColor Green
            Write-Host "Para filtrar contas:" -ForegroundColor Yellow
            Write-Host "curl -X GET `"$getAccountsEndpoint?type=ASSET&currency=BRL&isActive=true`"" -ForegroundColor Green
        }
        
        if ($createAccountEndpoint) {
            Write-Host "`nPara criar uma nova conta:" -ForegroundColor Yellow
            Write-Host "curl -X POST $createAccountEndpoint -H 'Content-Type: application/json' -d '{`"name`":`"Conta Teste`",`"type`":`"ASSET`",`"currency`":`"BRL`"}'" -ForegroundColor Green
        }

        if ($updateAccountEndpoint) {
            Write-Host "`nPara atualizar uma conta existente:" -ForegroundColor Yellow
            Write-Host "curl -X PUT `"$updateAccountEndpoint.Replace('{accountId}', 'ACC123')`" -H 'Content-Type: application/json' -d '{`"name`":`"Conta Atualizada`",`"type`":`"ASSET`",`"currency`":`"USD`"}'" -ForegroundColor Green
        }
        
        if ($createTransactionEndpoint) {
            Write-Host "`nPara criar uma nova transação:" -ForegroundColor Yellow
            Write-Host "curl -X POST $createTransactionEndpoint -H 'Content-Type: application/json' -d '{`"referenceId`":`"REF001`",`"description`":`"Transação de teste`",`"currency`":`"BRL`",`"entries`":[{`"accountId`":`"acc123`",`"entryType`":`"DEBIT`",`"amount`":100.00,`"description`":`"Débito`"},{`"accountId`":`"acc456`",`"entryType`":`"CREDIT`",`"amount`":100.00,`"description`":`"Crédito`"}]}'" -ForegroundColor Green
        }
        
        if ($getTransactionsEndpoint) {
            Write-Host "`nPara listar todas as transações:" -ForegroundColor Yellow
            Write-Host "curl -X GET $getTransactionsEndpoint" -ForegroundColor Green
            Write-Host "Para filtrar transações:" -ForegroundColor Yellow
            Write-Host "curl -X GET `"$getTransactionsEndpoint?startDate=2025-01-01&endDate=2025-12-31&status=COMPLETED&accountId=acc123`"" -ForegroundColor Green
        }
        
        if ($reverseTransactionEndpoint) {
            $reverseEndpointExample = $reverseTransactionEndpoint.Replace("{transactionId}", "TRANS123")
            Write-Host "`nPara estornar uma transação:" -ForegroundColor Yellow
            Write-Host "curl -X POST `"$reverseEndpointExample`"" -ForegroundColor Green
            Write-Host "Para estornar uma transação com descrição personalizada:" -ForegroundColor Yellow
            Write-Host "curl -X POST `"$reverseEndpointExample?description=Estorno manual da transação`"" -ForegroundColor Green
        }
        
        if ($transactionQueueUrl -and $processTransactionArn) {
            Write-Host "`nProcessamento de Transações:" -ForegroundColor Yellow
            Write-Host "A função ProcessTransaction ($processTransactionArn) está configurada para processar automaticamente as transações da fila $transactionQueueUrl" -ForegroundColor Green
        }
    }
    else {
        Write-Host "Não foi possível obter informações sobre os endpoints da API." -ForegroundColor Red
        
        Write-Host "`nEndpoints da API (padrão esperado):" -ForegroundColor Cyan
        Write-Host "GetAccountsApi: https://<api-id>.execute-api.$Region.amazonaws.com/Prod/api/v1/accounts" -ForegroundColor Green
        Write-Host "CreateAccountApi: https://<api-id>.execute-api.$Region.amazonaws.com/Prod/api/v1/accounts" -ForegroundColor Green
        Write-Host "UpdateAccountApi: https://<api-id>.execute-api.$Region.amazonaws.com/Prod/api/v1/accounts/{accountId}" -ForegroundColor Green
        Write-Host "CreateTransactionApi: https://<api-id>.execute-api.$Region.amazonaws.com/Prod/api/v1/transactions" -ForegroundColor Green
        Write-Host "GetTransactionsApi: https://<api-id>.execute-api.$Region.amazonaws.com/Prod/api/v1/transactions" -ForegroundColor Green
        Write-Host "ReverseTransactionApi: https://<api-id>.execute-api.$Region.amazonaws.com/Prod/api/v1/transactions/{transactionId}/reverse" -ForegroundColor Green
        Write-Host "TransactionQueueUrl: https://sqs.$Region.amazonaws.com/<account-id>/TransactionQueue" -ForegroundColor Green
        Write-Host "ProcessTransactionFunction: arn`:`aws`:`lambda`:`$Region`:`<account-id>`:`function`:`ProcessTransaction" -ForegroundColor Green
        
        Write-Host "`nPara obter os endpoints exatos, execute:" -ForegroundColor Yellow
        Write-Host "aws cloudformation describe-stacks --stack-name $StackName --region $Region --query 'Stacks[0].Outputs'" -ForegroundColor Green
    }
}

try {
    Write-Host "=== Iniciando deploy do Ledger Transacional ===" -ForegroundColor Cyan
    
    $bucketName = Ensure-S3Bucket -BucketName $S3BucketName
    Create-DynamoDBTables
    $queueUrl = Create-SQSQueue -QueueName $QueueName
    
    Build-Project
    Deploy-SAM -S3BucketName $bucketName
    
    Show-ApiInformation
    
    Write-Host "`n=== Deploy concluído com sucesso! ===" -ForegroundColor Cyan
}
catch {
    Write-Host "Erro durante o deploy: $_" -ForegroundColor Red
    exit 1
}