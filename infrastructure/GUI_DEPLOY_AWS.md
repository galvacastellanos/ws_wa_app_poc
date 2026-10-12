# Guía de despliegue (CloudFormation desde consola AWS)

Este documento describe qué hace cada template en `infrastructure/`, qué se necesita para ejecutarlo y cómo desplegarlo desde la interfaz gráfica de AWS.

## 1) Qué hace cada archivo YML

### `main.yml`
Template orquestador (nested stacks). Crea en orden:
- `networking.yml`
- `ecr.yml`
- `secrets.yml`
- `iam.yml`
- `github-actions-role.yml`
- `ecs.yml`

También conecta outputs/inputs entre stacks (VPC, subnets, roles, secret ARN, etc.).

### `templates/networking.yml`
Crea red base:
- VPC
- Internet Gateway
- Subnets públicas/privadas
- NAT Gateway
- Route tables y asociaciones

### `templates/ecr.yml`
Crea el repositorio ECR para imágenes Docker:
- `ws-wa-app-poc-v1-${EnvironmentName}`

### `templates/secrets.yml`
Crea el secreto en AWS Secrets Manager:
- guarda `ConnectionStrings__Default`
- expone `DatabaseSecretArn`

### `templates/iam.yml`
Crea roles IAM para ECS:
- Execution role
- Task role
- permisos para leer el secreto en Secrets Manager

### `templates/github-actions-role.yml`
Crea el rol OIDC para GitHub Actions:
- permisos para push a ECR
- permisos para actualizar ECS
- pass role para roles ECS

### `templates/ecs.yml`
Crea infraestructura de ejecución:
- ECS Cluster
- Task Definition
- ECS Service (Fargate)
- ALB + Target Group + Listener
- Security groups
- Log Group (CloudWatch Logs)

### `templates/alb.yml`
Template independiente de listener ALB (opcional).

> Nota: actualmente **no lo usa** `main.yml`, porque `ecs.yml` ya crea ALB/Listener.

---

## 2) Qué necesitas antes de ejecutar

1. Cuenta AWS con permisos para CloudFormation, IAM, EC2, ECS, ECR, ELBv2, Logs, Secrets Manager.
2. Región definida (por ejemplo `us-east-1`).
3. Bucket S3 para publicar templates.
4. Subir archivos al bucket respetando estructura:
   - `infrastructure/main.yml`
   - `infrastructure/templates/*.yml`
5. Imagen Docker disponible en ECR (o al menos un `ImageUri` válido para crear stack).
6. Valor real para conexión de base de datos en el secreto (después o durante update).
7. OIDC provider de GitHub en IAM (`token.actions.githubusercontent.com`) existente en la cuenta.

---

## 3) Cómo ejecutarlo desde la consola gráfica de AWS

## Opción recomendada: desplegar solo `main.yml`

1. Ir a **CloudFormation** > **Create stack** > **With new resources (standard)**.
2. En **Specify template** elegir **Upload a template file**.
3. Subir `infrastructure/main.yml`.
4. Completar parámetros:
   - `EnvironmentName`: `dev` o `prd`
   - `TemplatesBaseUrl`: URL base S3 donde está `infrastructure`.
	 - Ejemplo: `https://mi-bucket.s3.us-east-1.amazonaws.com/infrastructure`
   - `GitHubOwner`: usuario/org de GitHub
   - `GitHubRepository`: repo (ej. `ws_wa_app_remesas`)
   - `GitHubSubject`: claim exacto OIDC
	 - Ejemplo: `repo:galvacastellanos/ws_wa_app_remesas:ref:refs/heads/develop`
   - `ImageUri`: URI completo de imagen ECR con tag
	 - Ejemplo: `123456789012.dkr.ecr.us-east-1.amazonaws.com/ws-wa-app-poc-v1-dev:latest`
   - `ContainerPort`: `8080`
   - `HealthCheckPath`: `/health`
5. En **Configure stack options**, continuar con valores por defecto (o tags/políticas según necesidad).
6. En **Capabilities**, marcar:
   - `I acknowledge that AWS CloudFormation might create IAM resources`.
7. Crear stack y esperar `CREATE_COMPLETE`.

---

## 4) Ejecución individual de templates (manual)

Solo recomendado para pruebas. Debes respetar orden y pasar outputs manualmente:

1. `networking.yml`
2. `ecr.yml`
3. `secrets.yml`
4. `iam.yml` (requiere `DatabaseSecretArn`)
5. `github-actions-role.yml`
6. `ecs.yml` (requiere VPC/subnets/roles/secret/image)

---

## 5) Parámetros mínimos por entorno

Basado en `environments/dev.json` y `environments/prd.json`:
- `EnvironmentName`
- `TemplatesBaseUrl`
- `GitHubOwner`
- `GitHubRepository`
- `GitHubSubject`
- `ImageUri`
- `ContainerPort`
- `HealthCheckPath`

---

## 6) Verificación post-despliegue

1. CloudFormation stack principal en `CREATE_COMPLETE`.
2. En Outputs de `main` validar:
   - `EcrRepositoryUri`
   - `DatabaseSecretArn`
   - `EcsClusterName`
   - `EcsServiceName`
   - `ApplicationURL`
3. Probar `ApplicationURL` en navegador.
4. Revisar logs en CloudWatch si el servicio no levanta.

---

## 7) Errores frecuentes

- `TemplateURL` inválido: verificar ruta exacta en S3.
- `AccessDenied` IAM: falta capability o permisos del usuario que despliega.
- `InvalidClientTokenId`: credenciales inválidas/expiradas.
- ECS sin levantar tareas: imagen no existe en ECR o secret inválido.
- Health check fallando: ajustar `HealthCheckPath` y `ContainerPort`.
