# Etapa 1: Compilar el proyecto con Maven usando Debian (gRPC funciona nativo aquí)
FROM maven:3.9.6-eclipse-temurin-17 AS build
WORKDIR /app

# Copiar el archivo de configuración de dependencias (pom.xml)
COPY services/service-social/pom.xml ./services/service-social/
# Copiar el código fuente completo necesario para compilar
COPY services/service-social/src ./services/service-social/src

# Moverse al directorio del servicio y compilar
WORKDIR /app/services/service-social
RUN mvn clean package -DskipTests

# Etapa 2: Imagen ligera de ejecución para producción (Aquí sí usamos Alpine)
FROM eclipse-temurin:17-jre-alpine
WORKDIR /app

# Traer el archivo .jar compilado desde la etapa anterior
COPY --from=build /app/services/service-social/target/*.jar app.jar

# Exponer el puerto configurado para gRPC de la sección social
EXPOSE 9091

# Comando para arrancar el microservicio
ENTRYPOINT ["java", "-jar", "app.jar"]