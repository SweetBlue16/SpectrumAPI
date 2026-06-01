# Etapa 1: Compilar el proyecto con Maven
FROM maven:3.9.6-eclipse-temurin-17 AS build
WORKDIR /app

# COMPATIBILIDAD CRUCIAL PARA gRPC EN LINUX ALPINE:
# Instala las librerías necesarias para que el compilador de gRPC pueda ejecutarse.
RUN apk add --no-cache libc6-compat gcompat

# Copiar el archivo de configuración de dependencias (pom.xml)
COPY services/service-social/pom.xml ./services/service-social/
# Copiar el código fuente completo necesario para compilar
COPY services/service-social/src ./services/service-social/src

# Moverse al directorio del servicio y compilar omitiendo los tests para acelerar el proceso
WORKDIR /app/services/service-social
RUN mvn clean package -DskipTests

# Etapa 2: Imagen ligera de ejecución para producción
FROM eclipse-temurin:17-jre-alpine
WORKDIR /app

# Traer el archivo .jar compilado desde la etapa anterior
COPY --from=build /app/services/service-social/target/*.jar app.jar

# Exponer el puerto configurado para gRPC de la sección social
EXPOSE 9091

# Comando para arrancar el microservicio
ENTRYPOINT ["java", "-jar", "app.jar"]