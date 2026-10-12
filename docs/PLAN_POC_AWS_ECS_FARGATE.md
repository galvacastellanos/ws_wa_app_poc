# Plan POC AWS (ECS + Fargate) para `ws_wa_app_remesas`

## 1) Objetivo
Desplegar la API .NET en AWS usando contenedores con dos ambientes (`dev` y `prd`), 
con pipeline CI/CD desde GitHub Actions, imagen en ECR, ejecución en ECS Fargate detrás de ALB, y secretos/configuración en AWS Secrets Manager + SSM Parameter Store.

---

## 2) Arquitectura objetivo (bloques)
1. **Git (GitHub)**
2. **CI/CD Pipeline (GitHub Actions)**
   - Build
   - Test
   - Docker build
   - Push a ECR
   - Deploy ECS `dev`
   - Aprobación manual
   - Deploy ECS `prd`
3. **AWS Runtime**
   - ECS Cluster
   - Servicios Fargate (`dev` / `prd`)
   - ALB por ambiente (o uno compartido con reglas)
4. **Configuración y secretos**
   - SSM Parameter Store: configuración no sensible
   - Secrets Manager: cadena de conexión y secretos sensibles

---

## 3) Software y servicios necesarios

### Local (desarrollo)
- **Git**
- **.NET SDK 10**
- **Docker Desktop**
- **AWS CLI v2**
- **Session Manager Plugin** (opcional)
- **Visual Studio 2026** (ya disponible)

### AWS
- **ECR** (repositorio de imágenes)
- **ECS + Fargate** (ejecución de contenedores)
- **ALB** (entrada HTTP/HTTPS)
- **CloudWatch Logs** (logs)
- **Secrets Manager** (ConnectionString)
- **SSM Parameter Store** (configuración)
- **IAM** (roles para tareas y despliegue)
- **VPC/Subnets/Security Groups**

### CI/CD
- **GitHub Actions**
- OIDC de GitHub a AWS (recomendado, sin llaves estáticas)

---

## 4) Plan por bloques (implementación)

## Bloque A — Base del repositorio
1. Definir estrategia de ramas: `main` (prd), `develop` (dev), feature branches.
2. Agregar `.dockerignore`.
3. Crear `Dockerfile` multi-stage para .NET 10.
4. Estandarizar variables de entorno (`ASPNETCORE_ENVIRONMENT`, puertos, etc.).

**Entregables**
- `Dockerfile`
- `.dockerignore`
- Convención de ramas y versionado

## Bloque B — Contenedor local
1. Build local de imagen.
2. Run local para validar endpoints (`/swagger`, `/buscar`, health).
3. Ajustar configuración para leer variables de entorno.

**Comandos base**
- `docker build -t ws-wa-app-remesas:local .`
- `docker run -p 8080:8080 --env ASPNETCORE_ENVIRONMENT=Development ws-wa-app-remesas:local`

**Entregables**
- Imagen funcional local
- Checklist de validación local

## Bloque C — Infra AWS (POC)
1. Crear repositorio **ECR**.
2. Crear **ECS Cluster**.
3. Definir **Task Definition** Fargate (CPU/memoria, puerto, logs).
4. Crear **ECS Service dev** con ALB + target group.
5. Repetir para **prd** (aislado por servicio/cluster según costo y gobernanza).
6. Configurar autoscaling básico (mín 1, máx 2/3 tareas).

**Entregables**
- Cluster ECS
- Servicios `dev` y `prd`
- ALB(s) y DNS base

## Bloque D — Secretos y configuración
1. Guardar Connection String en **Secrets Manager**:
   - Ejemplo nombre: `/ws-wa-app-remesas/dev/sql-connection`
   - Ejemplo nombre: `/ws-wa-app-remesas/prd/sql-connection`
2. Guardar configuración no sensible en **SSM Parameter Store**.
3. Configurar Task Definition para inyectar:
   - `ConnectionStrings__DefaultConnection` desde Secrets Manager
   - otras claves de config desde SSM
4. Dar permisos IAM mínimos al **task execution role** y **task role**.

**Entregables**
- Secretos parametrizados por ambiente
- Inyección de settings validada en runtime

## Bloque E — CI (build/test/docker/push)
1. Workflow de GitHub Actions al hacer push/PR:
   - Restore
   - Build
   - Test
   - Publish
   - Docker build
   - Tag (`sha`, `latest-dev`, `latest-prd` según rama)
   - Push a ECR
2. Escaneo básico de imagen (opcional pero recomendado).

**Entregables**
- Workflow CI funcionando
- Imagen publicada en ECR por commit

## Bloque F — CD (deploy ECS)
1. Workflow de despliegue `dev` automático en `develop`.
2. `prd` con aprobación manual (GitHub Environments).
3. Update de ECS Service con nueva Task Definition (nueva imagen).
4. Verificación post-deploy (health check + smoke test).

**Entregables**
- Deploy continuo a `dev`
- Release controlada a `prd`

## Bloque G — Operación y observabilidad
1. Logs estructurados en CloudWatch.
2. Métricas y alarmas mínimas:
   - 5XX ALB
   - CPU/Memoria alta
   - Task restart
3. Runbook básico de rollback:
   - revertir task definition revision
   - redeploy versión previa

**Entregables**
- Dashboard/Alarmas básicas
- Procedimiento de rollback documentado

---

## 5) CI/CD a tener en cuenta (claves)
1. **Separación dev/prd**: variables, secretos, servicios ECS independientes.
2. **Seguridad**:
   - usar OIDC GitHub↔AWS (sin `AWS_ACCESS_KEY_ID` estático)
   - principio de mínimo privilegio IAM
3. **Trazabilidad**:
   - tag de imagen con SHA
   - release notes por despliegue
4. **Calidad previa a deploy**:
   - pruebas unitarias obligatorias
   - opcional: análisis SAST/dependencias
5. **Estrategia de despliegue**:
   - rolling update con `minimumHealthyPercent`
   - health checks estrictos
6. **Rollback rápido**:
   - conservar N revisiones de task definition

---

## 6) Estructura sugerida de archivos en el repo
- `.github/workflows/ci.yml`
- `.github/workflows/cd-dev.yml`
- `.github/workflows/cd-prd.yml`
- `Dockerfile`
- `.dockerignore`
- `deploy/ecs/taskdef-dev.json`
- `deploy/ecs/taskdef-prd.json`
- `docs/PLAN_POC_AWS_ECS_FARGATE.md` (este documento)

---

## 7) Orden de ejecución recomendado (resumen)
1. Contenerizar y validar local.
2. Crear ECR + push manual inicial.
3. Crear ECS/Fargate + ALB en `dev`.
4. Integrar Secrets Manager/SSM.
5. Automatizar CI.
6. Automatizar CD `dev`.
7. Clonar patrón para `prd` con aprobación manual.
8. Añadir observabilidad y rollback.

---

## 8) Criterios de éxito del POC
- API operativa en URL de ALB (`dev` y `prd`).
- Connection string consumida desde Secrets Manager (sin secreto en repo).
- Pipeline GitHub Actions construye, prueba, publica y despliega.
- Rollback probado a versión anterior.
