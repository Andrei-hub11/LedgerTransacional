# Undeploy-LedgerTransacional.ps1
# Script para remover toda a infraestrutura do Ledger Transacional de forma idempotente

param(
    [string]$StackName = "ledger-transacional",
    [string]$S3BucketName = "ledger-transacional-734154191343-us-east-1",
    [string]$Region = "us-east-1",
    [string]$AccountsTableName = "Accounts",
    [string]$TransactionsTableName = "Transactions",
    [string]$EntriesTableName = "Entries",
    [switch]$RemoveS3Bucket = $false,
    [switch]$Force = $false
)

try {
    $awsVersion = aws --version
    Write-Host "AWS CLI encontrado: $awsVersion" -ForegroundColor Green
}
catch {
    Write-Host "AWS CLI não encontrado. Por favor, instale o AWS CLI e configure suas credenciais." -ForegroundColor Red
    exit 1
}


function Confirm-Action {
    param(
        [string]$Message = "Deseja continuar?"
    )

    if ($Force) {
        return $true
    }

    $confirmation = Read-Host "$Message [S/N]"
    return $confirmation.ToLower() -eq "s"
}


function Remove-CloudFormationStack {
    param(
        [string]$StackName
    )

    Write-Host "Verificando se a stack '$StackName' existe..." -ForegroundColor Yellow
    $stackExists = $false

    try {
        $stackInfo = aws cloudformation describe-stacks --stack-name $StackName --region $Region 2>&1
        if ($LASTEXITCODE -eq 0) {
            $stackExists = $true
            Write-Host "Stack '$StackName' encontrada." -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Stack '$StackName' não existe." -ForegroundColor Yellow
        return
    }

    if ($stackExists) {
        if (Confirm-Action -Message "Deseja remover a stack '$StackName'?") {
            Write-Host "Removendo stack '$StackName'..." -ForegroundColor Yellow
            aws cloudformation delete-stack --stack-name $StackName --region $Region

            Write-Host "Aguardando a remoção da stack '$StackName'..." -ForegroundColor Yellow
            aws cloudformation wait stack-delete-complete --stack-name $StackName --region $Region

            if ($LASTEXITCODE -eq 0) {
                Write-Host "Stack '$StackName' removida com sucesso." -ForegroundColor Green
            }
            else {
                Write-Host "Erro ao remover stack '$StackName'. Verifique o console AWS para mais detalhes." -ForegroundColor Red
            }
        }
        else {
            Write-Host "Remoção da stack '$StackName' cancelada pelo usuário." -ForegroundColor Yellow
        }
    }
}


function Remove-DynamoDBTable {
    param(
        [string]$TableName
    )

    Write-Host "Verificando se a tabela '$TableName' existe..." -ForegroundColor Yellow
    $tableExists = $false

    try {
        $tableInfo = aws dynamodb describe-table --table-name $TableName --region $Region 2>&1
        if ($LASTEXITCODE -eq 0) {
            $tableExists = $true
            Write-Host "Tabela '$TableName' encontrada." -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Tabela '$TableName' não existe." -ForegroundColor Yellow
        return
    }

    if ($tableExists) {
        if (Confirm-Action -Message "Deseja remover a tabela '$TableName'?") {
            Write-Host "Removendo tabela '$TableName'..." -ForegroundColor Yellow
            aws dynamodb delete-table --table-name $TableName --region $Region

            Write-Host "Aguardando a remoção da tabela '$TableName'..." -ForegroundColor Yellow
            aws dynamodb wait table-not-exists --table-name $TableName --region $Region

            if ($LASTEXITCODE -eq 0) {
                Write-Host "Tabela '$TableName' removida com sucesso." -ForegroundColor Green
            }
            else {
                Write-Host "Erro ao remover tabela '$TableName'. Verifique o console AWS para mais detalhes." -ForegroundColor Red
            }
        }
        else {
            Write-Host "Remoção da tabela '$TableName' cancelada pelo usuário." -ForegroundColor Yellow
        }
    }
}


function Remove-S3Bucket {
    param(
        [string]$BucketName
    )

    
    if ([string]::IsNullOrEmpty($BucketName)) {
        $accountId = (aws sts get-caller-identity --query "Account" --output text)
        $BucketName = "ledger-transacional-$accountId-$Region"
        Write-Host "Nome do bucket não especificado. Usando nome padrão: $BucketName" -ForegroundColor Yellow
    }

    Write-Host "Verificando se o bucket '$BucketName' existe..." -ForegroundColor Yellow
    $bucketExists = $false

    try {
        $bucketCheck = aws s3api head-bucket --bucket $BucketName 2>&1
        if ($LASTEXITCODE -eq 0) {
            $bucketExists = $true
            Write-Host "Bucket '$BucketName' encontrado." -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Bucket '$BucketName' não existe." -ForegroundColor Yellow
        return
    }

    if ($bucketExists -and $RemoveS3Bucket) {
        if (Confirm-Action -Message "Deseja esvaziar e remover o bucket '$BucketName'? ATENÇÃO: Esta ação é irreversível!") {
            Write-Host "Esvaziando bucket '$BucketName'..." -ForegroundColor Yellow
            aws s3 rm "s3://$BucketName" --recursive --region $Region

            Write-Host "Removendo bucket '$BucketName'..." -ForegroundColor Yellow
            aws s3 rb "s3://$BucketName" --region $Region

            if ($LASTEXITCODE -eq 0) {
                Write-Host "Bucket '$BucketName' removido com sucesso." -ForegroundColor Green
            }
            else {
                Write-Host "Erro ao remover bucket '$BucketName'. Verifique o console AWS para mais detalhes." -ForegroundColor Red
            }
        }
        else {
            Write-Host "Remoção do bucket '$BucketName' cancelada pelo usuário." -ForegroundColor Yellow
        }
    }
    elseif ($bucketExists) {
        Write-Host "Bucket '$BucketName' não será removido (use -RemoveS3Bucket para remover)." -ForegroundColor Yellow
    }
}


try {
    Write-Host "=== Iniciando remoção da infraestrutura do Ledger Transacional ===" -ForegroundColor Cyan
    
    if (-not $Force) {
        Write-Host "`nATENÇÃO: Este script irá remover todos os recursos da aplicação Ledger Transacional" -ForegroundColor Red
        Write-Host "Isso inclui a stack CloudFormation e tabelas DynamoDB." -ForegroundColor Red
        
        if ($RemoveS3Bucket) {
            Write-Host "Também irá esvaziar e remover o bucket S3." -ForegroundColor Red
        }
        
        Write-Host "`nTodos os dados armazenados serão perdidos." -ForegroundColor Red
        
        if (-not (Confirm-Action -Message "`nDeseja prosseguir com a remoção?")) {
            Write-Host "Operação cancelada pelo usuário." -ForegroundColor Yellow
            exit 0
        }
    }
    
    
    Remove-CloudFormationStack -StackName $StackName
    
    Remove-DynamoDBTable -TableName $AccountsTableName
    Remove-DynamoDBTable -TableName $TransactionsTableName
    Remove-DynamoDBTable -TableName $EntriesTableName
    
    if ($RemoveS3Bucket) {
        Remove-S3Bucket -BucketName $S3BucketName
    }
    else {
        Write-Host "O bucket S3 não será removido. Use o parâmetro -RemoveS3Bucket para removê-lo." -ForegroundColor Yellow
    }
    
    Write-Host "`n=== Remoção concluída! ===" -ForegroundColor Cyan
}
catch {
    Write-Host "Erro durante a remoção: $_" -ForegroundColor Red
    exit 1
}