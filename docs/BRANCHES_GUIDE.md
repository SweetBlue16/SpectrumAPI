# ESTÁNDAR DE RAMAS Y COMANDOS: GUÍA COMPLETA

Para mantener el repositorio organizado y el código a salvo, todas las ramas deben seguir esta convención de nomenclatura y buenas prácticas.

## 1. Ramas principales
* ***main:*** Código en producción, siempre debe estar estable.
* ***develop:*** Código de integración. Aquí se juntan todas las ramas antes de pasar a main.

## 2. Prefijos de ramas de trabajo

**Estructura:** `<tipo>/<ticket-opcional>-<descripcion-corta>`

* ***feat/:*** Nueva funcionalidad.
    * Ej: `feat/reviews-module`
* ***fix/:*** Corrección de un error.
    * Ej: `fix/wcf-login-error`
* ***hotfix/:*** Corrección crítica urgente en producción.
    * Ej: `hotfix/database-down`
* ***docs/:*** Exclusiva para documentación.
    * Ej: `docs/swagger-update`
* ***refactor/:*** Reestructuración de código sin agregar funciones.
    * Ej: `refactor/wpf-architecture-migration`

## 3. Comandos básicos (flujo de trabajo diario)

1. Ver tus ramas locales: 
`git branch`
2. Crear una rama nueva y moverte a ella: 
`git switch -c feat/new-function`
3. Moverte a una rama existente: 
`git switch develop`
4. Subir tu nueva rama al servidor (primer push):
`git push -u origin feat/new-function`
5. Fusionar una rama (estando posicionado en la rama que recibe):
`git merge feat/new-function`

## 4. Comandos intermedios (resolución de problemas)

* **GUARDAR TRABAJO A MEDIAS:**
Si necesitas cambiar de rama pero tu código está roto y no quieres hacer un commit:
    - `git stash`: Guarda tus cambios temporalmente y limpia la rama.
    - `git stash pop`: Saca los cambios del cajón y te los devuelve.

* **ACTUALIZAR EL MAPA DEL SERVIDOR:**
Para ver qué ramas nuevas o commits han subido tus compañeros sin modificar tus archivos locales:
    - `git fetch`

* **BORRAR RAMAS LUEGO DE FUSIONARLAS:**
Para no acumular basura en tu computadora:
    - `git branch -d <nombre-rama>`: Borrado seguro, verifica si se fusionó.
    - `git branch -D <nombre-rama>`: Borrado forzado, pierdes los cambios.

* **RENOMBRAR LA RAMA ACTUAL:**
Si te equivocaste al escribir el nombre al crearla:
    - `git branch -m <nuevo-nombre>`

* **VER EL HISTORIAL COMO UN ÁRBOL:**
Dibuja las ramificaciones y los commits en la terminal:
    - `git log --graph --oneline --all`

## 5. Buenas prácticas generales

- Usa todo en **minúsculas** y guiones medios (-) para separar palabras.
- **Mantén tu rama actualizada:** haz `git merge develop` hacia tu rama de trabajo frecuentemente para no quedar rezagado.
- **Crear ramas atómicas:** Una rama = una sola característica o corrección.