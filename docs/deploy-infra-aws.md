# Despliegue de infraestructura en AWS (CloudFormation)

## Alcance real según la carpeta `infrastructure`
El despliegue principal se hace con `infrastructure/main.yml` y crea estos nested stacks:
- `templates/networking.yml`
- `templates/ecr.yml`
- `templates/secrets.yml`
- `templates/iam.yml`
- `templates/github-actions-role.yml`
- `templates/ecs.yml`

> `templates/alb.yml` existe, pero actualmente **no lo invoca** `main.yml` porque el ALB/Listener ya se crea dentro de `ecs.yml`.

## 1) Qué subir a S3
Debes subir manteniendo estructura de carpetas:
- `infrastructure/main.yml`
- `infrastructure/templates/*.yml`

Ejemplo de base URL esperada por `main.yml`:
- `https://<bucket>.s3.<region>.amazonaws.com/infrastructure`

Con esa base, `main.yml` resolverá rutas como:
- `${TemplatesBaseUrl}/templates/networking.yml`
- `${TemplatesBaseUrl}/templates/ecs.yml`

## 2) Parámetros obligatorios de `main.yml`
Al crear el stack desde la consola de CloudFormation:
- `EnvironmentName`: `dev` o `prd`
- `TemplatesBaseUrl`: URL base S3 de `infrastructure`
- `GitHubOwner`: por ejemplo `galvacastellanos`
- `GitHubRepository`: por ejemplo `ws_wa_app_remesas`
- `GitHubSubject`: claim OIDC exacto (ej. branch)
- `ImageUri`: URI de imagen (opcional en bootstrap; tiene default temporal)
- `DesiredCount`: cantidad inicial de tareas ECS (recomendado `0` en bootstrap)
- `ContainerPort`: por defecto `8080`
- `HealthCheckPath`: por defecto `/health`

Referencias de ejemplo ya definidas:
- `infrastructure/environments/dev.json`
- `infrastructure/environments/prd.json`

### 2.1) ¿De dónde sale cada parámetro?
- `EnvironmentName`:
  - Lo defines tú según el despliegue: `dev` o `prd`.
  - Debe coincidir con los valores permitidos en `main.yml` y templates hijos.

- `TemplatesBaseUrl`:
  - Sale de la URL S3 donde subiste la carpeta `infrastructure`.
  - Formato esperado: `https://<bucket>.s3.<region>.amazonaws.com/infrastructure`.

- `GitHubOwner`:
  - Lo obtienes del owner del repositorio en GitHub.
  - Ejemplo: en `github.com/galvacastellanos/ws_wa_app_remesas`, el owner es `galvacastellanos`.

- `GitHubRepository`:
  - Es el nombre del repositorio en GitHub.
  - Ejemplo: `ws_wa_app_remesas`.

- `GitHubSubject`:
  - Se define en base al branch/evento autorizado para OIDC.
  - Formato común por branch: `repo:<owner>/<repo>:ref:refs/heads/<branch>`.
  - Ejemplo `main`: `repo:galvacastellanos/ws_wa_app_remesas:ref:refs/heads/main`.

- `ImageUri`:
  - Para bootstrap inicial no es obligatorio definir uno real: `main.yml` y `ecs.yml` tienen default temporal.
  - Cuando GitHub Actions publique la imagen, actualiza el stack con el `ImageUri` real de ECR.
  - Formato final esperado: `<account-id>.dkr.ecr.<region>.amazonaws.com/<repo>:<tag>`.

- `DesiredCount`:
  - Para bootstrap usar `0` (evita que ECS intente levantar tareas sin imagen final).
  - Después de publicar imagen, subir a `1` (o más) vía update de CloudFormation o desde el pipeline.

- `ContainerPort`:
  - Sale del puerto expuesto por tu contenedor/app (.NET).
  - Actualmente el valor por defecto es `8080` y debe coincidir con tu Dockerfile/app.

- `HealthCheckPath`:
  - Sale del endpoint de salud de la aplicación.
  - Actualmente el valor por defecto es `/health` y debe responder 200-399.

## 3) Prerrequisitos AWS
Antes de desplegar:
- Credenciales AWS válidas (token no expirado)
- Región seleccionada
- Permisos para CloudFormation, IAM, EC2, ECS, ECR, ELBv2, Logs, Secrets Manager

### 3.1) Crear/validar proveedor OIDC de GitHub en IAM (manual)
En cada cuenta (DEV y PRD):
1. Ir a **IAM** → **Proveedores de identidad** → **Agregar proveedor**.
2. Elegir tipo **OpenID Connect**.
3. En **URL de proveedor** ingresar:
   - `https://token.actions.githubusercontent.com`
4. En **Público (Audience / Client ID)** ingresar:
   - `sts.amazonaws.com`
5. Guardar con **Agregar proveedor**.

Si ya existe un proveedor con esa URL, no crear otro duplicado.

### 3.2) Relación con el parámetro `GitHubSubject`
El proveedor OIDC permite la federación, y `GitHubSubject` restringe qué workflow/branch puede asumir el rol.

Ejemplos de `GitHubSubject`:
- rama `main`: `repo:galvacastellanos/ws_wa_app_remesas:ref:refs/heads/main`
- rama `develop`: `repo:galvacastellanos/ws_wa_app_remesas:ref:refs/heads/develop`

## 4) Ejecución desde la consola gráfica
1. CloudFormation → **Create stack** → **With new resources (standard)**.
2. Subir `infrastructure/main.yml`.
3. Completar parámetros (sección anterior).
4. En capabilities, marcar creación de recursos IAM.
5. Crear stack y esperar `CREATE_COMPLETE`.

## 5) Qué crea cada template
- `networking.yml`: VPC, subnets públicas/privadas, IGW, NAT, route tables.
- `ecr.yml`: repositorio ECR `ws-wa-app-poc-v1-${EnvironmentName}`.
- `secrets.yml`: secreto DB (`ConnectionStrings__Default`) y su ARN.
- `iam.yml`: roles ECS Execution y Task con acceso al secreto.
- `github-actions-role.yml`: rol OIDC para GitHub Actions (ECR push + ECS deploy).
- `ecs.yml`: cluster ECS Fargate, task definition, service, ALB, target group, listener, security groups y logs.

## 6) Validaciones post-despliegue
Revisar outputs del stack principal:
- `EcrRepositoryUri`
- `DatabaseSecretArn`
- `EcsExecutionRoleArn`
- `EcsTaskRoleArn`
- `GitHubActionsRoleArn`
- `EcsClusterName`
- `EcsServiceName`
- `ApplicationURL`

## 7) Ajustes recomendados después del primer deploy
- Actualizar el secreto creado en Secrets Manager con una conexión real de base de datos (en `secrets.yml` viene placeholder).
- Publicar imagen desde GitHub Actions a ECR y luego actualizar `ImageUri` en el stack principal.
- Cambiar `DesiredCount` a `1` (o más) después de tener imagen válida.
- Validar que `GitHubSubject` coincida exactamente con el branch/evento que usará GitHub Actions.
