# CONVENTIONAL COMMITS: GUÍA RÁPIDA

Esta es la convención estándar para redactar el historial del proyecto. Cada commit debe contar una historia clara de qué cambió y por qué.

## 1. Estructura básica
```git
<tipo>(<ámbito opcional>): <descripción corta>
<LÍNEA EN BLANCO>
[cuerpo detallado opcional]
<LÍNEA EN BLANCO>
[pie de página opcional]
```

## 2. Tipos de commits (El "qué")

Usa estos prefijos obligatorios para categorizar tu trabajo:
* ***feat:*** Una nueva característica o funcionalidad.
    * Ej: `feat(api): add endpoint to fetch users`
* ***fix:*** Corrección de un error o bug.
    * Ej: `fix(auth): resolve WCF exception during login`
* ***test:*** Añadir o corregir pruebas unitarias/integración.
    * Ej: `test(core): add unit tests for AHP feasibility calculation`
* ***refactor:*** Cambios en el código que no añaden funciones ni corrigen bugs, sino que mejoran la estructura o arquitectura.
    * Ej: `refactor(db): decouple Entity Framework Core from business logic`
* ***docs:*** Cambios exclusivos en la documentación, en el README, Swagger, etc.
    * Ej: `docs: update deployment instructions in README`
* ***style:*** Cambios visuales en el código como formateo, espacios o comillas.
    * Ej: `style: format data access layer files`
* ***perf:*** Mejoras de rendimiento en el código existentes.
    * Ej: `perf(ui): optimize grid rendering in WPF`
* ***chore:*** Tareas de mantenimiento, actualización de dependencias o configuración de herramientas.
    * Ej: `chore: update .NET 8 packages to latest version`
* ***ci:*** Cambios en los archivos de integración continua (GitHub Actions).
    * Ej: `ci: add commit validation with husky`

## 3. Ámbito / Scope (opcional)
Va entre paréntesis y en minúsculas. Indica la capa, módulo o componente afectado para dar contexto rápido.

**Ejemplos comunes:** (api), (ui), (db), (auth), (router), (core)

## 4. Reglas para la descripción corta
- Máximo **50 caracteres**.
- Usa **modo imperativo**, como si dieras una orden.
    - ***BIEN:*** "add", "fix", "remove", "update".
    - ***MAL:*** "added", "fixing", "removed".
- Todo en **minúsculas** (preferentemente).
- Sin punto final.

**REGLA DE ORO:** If applied, this commit will: `<descripción>`

Ej: **[If applied, this commit will] "add JWT validation"**

## 5. El cuerpo y pie de página
Si el cambio es grande y el título no basta, deja una línea en blanco y explica el **POR QUÉ** y el **QUÉ**. El CÓMO se ve en el código.

***CUERPO:***
Explica el problema original y la solución adoptada. Trata de que las líneas no pasen de 72 caracteres de largo para que sea legible en cualquier terminal.

***PIE DE PÁGINA:***
- Úsalo para indicar si hay BREAKING CHANGES (cambios que rompen la compatibilidad con versiones anteriores).
- Úsalo para cerrar tickets automáticamente en GitHub/Jira.

## 6. Buenas prácticas generales
***COMMITS ATÓMICOS (Pequeños y enfocados):*** Un commit debe representar **UN SOLO** cambio lógico. Es preferible tener 10 commits pequeños, fáciles de leer y de revertir, que un solo commit gigante que modifique 30 archivos a la vez. 
  
***Regla práctica:*** No mezcles la creación de una tabla en la base de datos con el cambio de color de un botón en el mismo commit. Divídelo en dos: un feat(db) y un style(ui).

## Ejemplo de un commit completo y perfecto
feat(auth): implement role-based system with JWT

Previously, users had global access to all API endpoints, 
which represented a security vulnerability.

JWT token generation and validation were integrated into the middleware. 
Now every request requires a valid token in the Authorization header.

BREAKING CHANGE: Frontend clients must now send the Authorization
header with the Bearer prefix.

Closes #45
