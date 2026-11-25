# Listado de casos de prueba y estado de desarrollo

## Criterio de clasificación

- `[x]`: escenario desarrollado con comprobación del resultado en una prueba automatizada existente. Puede estar cubierto dentro de otra prueba; no implica un método independiente por cada entrada.
- `[ ]`: pendiente de desarrollo o con cobertura parcial; las coberturas parciales se indican expresamente.
- `[--]`: escenario implícito dentro de otro caso; se indica al final `[Implícito en caso XXXXX]` con el ID del caso que lo cubre.
- **No aplica**: caso excluido del alcance por indicación del usuario.
- Las referencias enlazan al archivo de pruebas y muestran el ID de `[Description]`.
- Este inventario corresponde al código revisado, no al resultado de ejecuciones. Un check no significa que la prueba haya pasado.
- Los Page Objects, datos, comentarios y archivos de trazabilidad por sí solos no se consideran casos desarrollados.
- Se conservan todos los escenarios solicitados. Los casos genéricos «Consulta sin resultados» y «Limpieza de filtros» se documentan con su implementación actual en consulta de documentos, indicando la ausencia de cobertura equivalente en tipos de documento.

## Resumen de estado

- **Pruebas automatizadas existentes en el código:** 64 (Login 4, Logout 2, GlobalDashboard 4, Consulta de documentos 6, Tipo de documentos 2, Cargar documentos 10, Consulta de imágenes 8, Cargar imágenes 12, Sha256 16).
- **Escenarios marcados `[x]`:** 64, uno por prueba automatizada.
- **Escenarios implícitos `[--]`:** 15, marcados con la indicación «Implícito en caso» y el ID del caso que los cubre.
- **Pendientes o con cobertura parcial (`[ ]`):** 10 (Login 5, Sesión y pantalla de inicio 2, Cargar documentos 2, Cargar imágenes 1).
- **No aplica:** 60, excluidos del alcance.

## Módulo: Login

- [x] Inicio de sesión con credenciales válidas — [TC-LOGIN-01](../Tests/Login/LoginTests.cs).
- [x] Inicio de sesión con credenciales inválidas — [TC-LOGIN-02](../Tests/Login/LoginTests.cs).
- **No aplica** Login con usuario vacío — Pendiente.
- **No aplica** Login con contraseña vacía — Pendiente.
- **No aplica** Login con código inválido — Pendiente.
- [x] Inicio de sesión con campos vacíos — [TC-LOGIN-03](../Tests/Login/LoginTests.cs).
- [ ] Validación de generación de token — Pendiente.
- [ ] Validación de estructura del token — Pendiente.
- [ ] Uso de token válido en endpoints — Pendiente.
- [ ] Uso de token inválido — Pendiente.
- **No aplica** — Uso de token expirado (60 min)
- [ ] Consumo sin autenticación — Pendiente.

## Módulo: Login — Sesión, cierre de sesión y pantalla de inicio

- [ ] Inicio de sesión con método MiSSO — Pendiente.
- [x] Acceso sin autenticación al portal — [TC-LOGIN-04](../Tests/Login/LoginTests.cs), mediante acceso directo a una URL protegida.
- [x] Acceso sin autenticación a la URL global — [TC-GLOBALDASHBOARD-04](../Tests/GlobalDashboard/UnauthorizedAccessTests.cs).
- [--] Persistencia de sesión activa — [Implícito en caso TC-LOGIN-06]
- **No aplica** — Expiración de sesión por inactividad
- **No aplica** — Uso del sistema con sesión expirada
- [x] Cierre de sesión exitoso — [TC-LOGIN-05](../Tests/Login/LogoutTests.cs).
- [x] Cancelación de cierre de sesión — [TC-LOGIN-06](../Tests/Login/LogoutTests.cs).
- [--] Redirección al login después de logout — [Implícito en caso TC-LOGIN-05]
- **No aplica** — Inicio de sesión concurrente en múltiples sesiones
- **No aplica** — Acceso con usuario deshabilitado
- [--] Visualización de pantalla de inicio — [Implícito en caso Visualización de mensaje de bienvenida]
- [ ] Visualización de mensaje de bienvenida — Pendiente.
- **No aplica** — Validación de nombre del usuario autenticado
- [x] Visualización de fecha y hora — [TC-GLOBALDASHBOARD-01](../Tests/GlobalDashboard/DateTimeDisplayTests.cs), con fecha del día y tolerancia de dos minutos para la hora.
- [x] Visualización del menú lateral — [TC-GLOBALDASHBOARD-02](../Tests/GlobalDashboard/SidebarVisibilityTests.cs).
- **No aplica** — Navegación entre módulos
- **No aplica** — Acceso a módulos sin permisos
- [x] Visualización de estado de sesión — [TC-GLOBALDASHBOARD-05](../Tests/GlobalDashboard/SessionStatusTests.cs), mensaje «Sesión activa».

## Módulo: Documentos / Consulta

- [--] Consulta de documentos con filtros básicos — [Implícito en caso TC-DOCUMENTOS-CONSULTADOCUMENTOS-01 a 04]
  - [x] Consulta de documento con "Serial documento" — [TC-DOCUMENTOS-CONSULTADOCUMENTOS-01](../Tests/Documentos/DocumentosSearchTests.cs).
  - [x] Consulta de documento con "Tipo documento" — [TC-DOCUMENTOS-CONSULTADOCUMENTOS-02](../Tests/Documentos/DocumentosSearchTests.cs).
  - [x] Consulta de documento con "Path documento" — [TC-DOCUMENTOS-CONSULTADOCUMENTOS-03](../Tests/Documentos/DocumentosSearchTests.cs).
  - [x] Consulta de documento con "Nombre documento" — [TC-DOCUMENTOS-CONSULTADOCUMENTOS-04](../Tests/Documentos/DocumentosSearchTests.cs).
- **No aplica** — Consulta con filtros avanzados
- [x] Consulta sin resultados — [TC-DOCUMENTOS-CONSULTADOCUMENTOS-05](../Tests/Documentos/DocumentosSearchTests.cs), mediante serial inexistente y mensaje sin resultados.
- [x] Limpieza de filtros — [TC-DOCUMENTOS-CONSULTADOCUMENTOS-06](../Tests/Documentos/DocumentosSearchTests.cs).
- **No aplica** — Búsqueda por fechas
- **No aplica** — Búsqueda por múltiples criterios
- **No aplica** — Visualización de listado
- **No aplica** — Visualización de detalle de documento
- **No aplica** — Validación de metadata
- **No aplica** — Validación de vigencias
- [--] Consulta de documento inexistente — [Implícito en caso TC-DOCUMENTOS-CONSULTADOCUMENTOS-05]

## Módulo: Documentos / Tipo de documentos

- **No aplica** — Consulta de tipos de documento
- **No aplica** — Creación de tipo válido
- [x] Creación con campos vacíos — [TC-DOCUMENTOS-DOCUMENTTYPECREATION-05](../Tests/Documentos/DocumentTypeCreationTests.cs).
- [x] Creación duplicada — [TC-DOCUMENTOS-DOCUMENTTYPECREATION-06](../Tests/Documentos/DocumentTypeCreationTests.cs).
- **No aplica** — Edición de tipo
- **No aplica** — Búsqueda de tipos
- **No aplica** — Eliminación de tipo en uso

## Módulo: Documentos / Cargar

- **No aplica** — Validación de listado de tipos de documento
- **No aplica** — Selección de tipo de documento válido
- [x] Carga de documento válida — [TC-DOCUMENTOS-CARGARDOCUMENTOS-01](../Tests/Documentos/DocumentosUploadTests.cs), con captura del serial y consulta posterior.
- [ ] Carga de documento sin seleccionar tipo de documento — Pendiente.
- **No aplica** — Carga de documento con tipo de documento inactivo
- [ ] Carga de documento sin informar Path del documento — Pendiente.
- **No aplica** — Carga de documento con Path válido/Documento ya cargado (mensaje de actualización)
- [x] Carga de documento con Path inexistente (usar path `cia\pathfalso123`) — [TC-DOCUMENTOS-CARGARDOCUMENTOS-02](../Tests/Documentos/DocumentosUploadTests.cs). El resultado esperado actual es carga exitosa, no rechazo del path.
- [x] Carga de documento con caracteres especiales en Path — [TC-DOCUMENTOS-CARGARDOCUMENTOS-03](../Tests/Documentos/DocumentosUploadTests.cs).
- [x] Carga de documento sin seleccionar archivo — [TC-DOCUMENTOS-CARGARDOCUMENTOS-04](../Tests/Documentos/DocumentosUploadTests.cs), botón deshabilitado.
- [x] Carga de archivo PDF válido menor a 5 MB — [TC-DOCUMENTOS-CARGARDOCUMENTOS-05](../Tests/Documentos/DocumentosUploadTests.cs).
- [x] Carga de archivo PDF de exactamente 5 MB — [TC-DOCUMENTOS-CARGARDOCUMENTOS-06](../Tests/Documentos/DocumentosUploadTests.cs), comprueba ausencia de error de tamaño y botón habilitado.
- [x] Carga de archivo PDF superior a 5 MB — [TC-DOCUMENTOS-CARGARDOCUMENTOS-07](../Tests/Documentos/DocumentosUploadTests.cs), rechazo al seleccionar el archivo.
- [x] Carga de archivo en formato diferente a PDF — [TC-DOCUMENTOS-CARGARDOCUMENTOS-08](../Tests/Documentos/DocumentosUploadTests.cs).
- [x] Carga de PDF protegido con contraseña — [TC-DOCUMENTOS-CARGARDOCUMENTOS-09](../Tests/Documentos/DocumentosUploadTests.cs).
- [x] Carga de PDF corrupto — [TC-DOCUMENTOS-CARGARDOCUMENTOS-10](../Tests/Documentos/DocumentosUploadTests.cs).
- **No aplica** — Carga de documento con todos los campos opcionales diligenciados
- **No aplica** — Carga de documento únicamente con campos obligatorios
- **No aplica** — Carga de documento con fecha de vencimiento válida
- **No aplica** — Carga de documento con fecha de vencimiento vencida
- **No aplica** — Carga de documento con formato de fecha inválido
- **No aplica** — Carga de documento con Id enrolamiento válido
- **No aplica** — Cambio de estado del documento
- **No aplica** — Consulta posterior del documento cargado

## Módulo: Documentos / Consulta de imágenes

- **No aplica** — Consulta de imágenes sin filtros
- **No aplica** — Consulta de imágenes con resultado exitoso
- [x] Consulta de imágenes sin resultados — [TC-DOCUMENTOS-CONSULTARIMAGENES-05](../Tests/Documentos/ConsultarImagenesTests.cs).
- [x] Búsqueda por Serial imagen — [TC-DOCUMENTOS-CONSULTARIMAGENES-01](../Tests/Documentos/ConsultarImagenesTests.cs).
- [x] Búsqueda por Path imagen — [TC-DOCUMENTOS-CONSULTARIMAGENES-03](../Tests/Documentos/ConsultarImagenesTests.cs).
- **No aplica** — Búsqueda usando múltiples filtros
- [x] Búsqueda por Fecha creación desde — [TC-DOCUMENTOS-CONSULTARIMAGENES-07](../Tests/Documentos/ConsultarImagenesTests.cs), fecha futura y resultado vacío.
- [x] Búsqueda por Fecha creación hasta — [TC-DOCUMENTOS-CONSULTARIMAGENES-08](../Tests/Documentos/ConsultarImagenesTests.cs), fecha anterior a los registros y resultado vacío.
- **No aplica** — Ocultar y mostrar filtros de búsqueda
- **No aplica** — Validación de caracteres especiales en filtros
- **No aplica** — Validación de longitudes máximas en campos de búsqueda
- **No aplica** — Visualización de detalle de imagen
- **No aplica** — Descarga de imagen desde acciones
- **No aplica** — Validación de paginación de resultados
- **No aplica** — Validación de ordenamiento de resultados
- **No aplica** — Consulta de imágenes con permisos válidos
- **No aplica** — Consulta de imágenes sin permisos
- [x] Búsqueda por Id imagen — [TC-DOCUMENTOS-CONSULTARIMAGENES-02](../Tests/Documentos/ConsultarImagenesTests.cs).
- [x] Búsqueda por Nombre imagen — [TC-DOCUMENTOS-CONSULTARIMAGENES-04](../Tests/Documentos/ConsultarImagenesTests.cs).
- [x] Limpieza de filtros en consulta de imágenes — [TC-DOCUMENTOS-CONSULTARIMAGENES-06](../Tests/Documentos/ConsultarImagenesTests.cs).

## Módulo: Documentos / Cargar imágenes

- [x] Carga de imagen válida — [TC-DOCUMENTOS-CARGARIMAGENES-01](../Tests/Documentos/DocumentosImageUploadTests.cs).
- [x] Carga de imagen sin informar Path de la imagen (limpiar el campo del path) — [TC-DOCUMENTOS-CARGARIMAGENES-02](../Tests/Documentos/DocumentosImageUploadTests.cs), comprueba que no aparece mensaje de éxito; no exige un mensaje específico de campo obligatorio.
- [x] Carga de imagen con Path válido — [TC-DOCUMENTOS-CARGARIMAGENES-03](../Tests/Documentos/DocumentosImageUploadTests.cs).
- [x] Carga de imagen sin seleccionar archivo — [TC-DOCUMENTOS-CARGARIMAGENES-04](../Tests/Documentos/DocumentosImageUploadTests.cs), botón deshabilitado.
- [x] Carga de archivo PNG válido — [TC-DOCUMENTOS-CARGARIMAGENES-05](../Tests/Documentos/DocumentosImageUploadTests.cs).
- [x] Carga de archivo JPG válido — [TC-DOCUMENTOS-CARGARIMAGENES-06](../Tests/Documentos/DocumentosImageUploadTests.cs).
- [x] Carga de archivo JPEG válido — [TC-DOCUMENTOS-CARGARIMAGENES-07](../Tests/Documentos/DocumentosImageUploadTests.cs).
- **No aplica** — Carga de archivo de exactamente 3 MB
- [x] Carga de archivo superior a 3 MB — [TC-DOCUMENTOS-CARGARIMAGENES-09](../Tests/Documentos/DocumentosImageUploadTests.cs).
- [x] Carga de archivo PDF — [TC-DOCUMENTOS-CARGARIMAGENES-10](../Tests/Documentos/DocumentosImageUploadTests.cs), rechazo del formato.
- [x] Carga de archivo con extensión no permitida — [TC-DOCUMENTOS-CARGARIMAGENES-11](../Tests/Documentos/DocumentosImageUploadTests.cs).
- [x] Carga de archivo corrupto — [TC-DOCUMENTOS-CARGARIMAGENES-12](../Tests/Documentos/DocumentosImageUploadTests.cs).
- [x] Carga de imagen con nombre duplicado misma ruta — [TC-DOCUMENTOS-CARGARIMAGENES-13](../Tests/Documentos/DocumentosImageUploadTests.cs), comprueba el mensaje de reemplazo en la segunda carga.
- **No aplica** — Carga de imagen con todos los campos opcionales diligenciados
- **No aplica** — Carga de imagen únicamente con campos obligatorios
- **No aplica** — Carga de imagen con Documento identidad
- **No aplica** — Carga de imagen con Id tipo documento identidad
- **No aplica** — Carga de imagen con Id centro
- **No aplica** — Carga de imagen con caracteres especiales en los campos informativos
- **No aplica** — Carga de imagen con longitud máxima permitida en los campos
- **No aplica** — Validación de habilitación del botón "Cargar imagen"
- **No aplica** — Cancelación de selección de archivo
- **No aplica** — Persistencia de la información cargada
- **No aplica** — Consulta posterior de la imagen cargada
- [ ] Visualización de la imagen cargada desde el módulo Consultar imágenes — Pendiente.

## Módulo: Documentos / Sha256

- [x] Selección de validación de Documento — [TC-DOCUMENTOS-SHA256-02](../Tests/Documentos/Sha256Tests.cs).
- [x] Selección de validación de Imagen — [TC-DOCUMENTOS-SHA256-03](../Tests/Documentos/Sha256Tests.cs).
- [x] Cambio entre tipos de validación Documento e Imagen — [TC-DOCUMENTOS-SHA256-04](../Tests/Documentos/Sha256Tests.cs), comprueba el reinicio del serial.
- [x] Búsqueda por serial válido — [TC-DOCUMENTOS-SHA256-05](../Tests/Documentos/Sha256Tests.cs), Documento e Imagen; valida serial, nombre y SHA256 del registro.
- [x] Búsqueda por serial inexistente — [TC-DOCUMENTOS-SHA256-06](../Tests/Documentos/Sha256Tests.cs), Documento e Imagen.
- [x] Búsqueda con serial vacío — [TC-DOCUMENTOS-SHA256-07](../Tests/Documentos/Sha256Tests.cs), Documento e Imagen.
- [x] Búsqueda con formato de serial inválido — [TC-DOCUMENTOS-SHA256-08](../Tests/Documentos/Sha256Tests.cs), Documento e Imagen.
- [x] Limpieza del serial mediante botón Limpiar — [TC-DOCUMENTOS-SHA256-09](../Tests/Documentos/Sha256Tests.cs), Documento e Imagen.
- [x] Cancelación de la búsqueda — [TC-DOCUMENTOS-SHA256-10](../Tests/Documentos/Sha256Tests.cs), Documento e Imagen.
- [--] Visualización de información del registro encontrado — [Implícito en caso TC-DOCUMENTOS-SHA256-05]
- [x] Carga de archivo válido para validación — [TC-DOCUMENTOS-SHA256-01](../Tests/Documentos/Sha256Tests.cs), adjunta un PDF válido y comprueba que el botón «Validar documento» queda habilitado.
- [x] Carga sin seleccionar archivo — [TC-DOCUMENTOS-SHA256-11](../Tests/Documentos/Sha256Tests.cs), «Validar» deshabilitado.
- [x] Carga de archivo superior al tamaño permitido — [TC-DOCUMENTOS-SHA256-12](../Tests/Documentos/Sha256Tests.cs), 5 MB Documento y 3 MB Imagen.
- [x] Carga de archivo con formato no permitido — [TC-DOCUMENTOS-SHA256-13](../Tests/Documentos/Sha256Tests.cs), Documento e Imagen.
- **No aplica** — Carga de archivo corrupto
- [x] Validación de archivo con SHA256 coincidente — [TC-DOCUMENTOS-SHA256-14](../Tests/Documentos/Sha256Tests.cs), archivo original descargado desde «Ver archivo».
- [x] Validación de archivo con SHA256 diferente — [TC-DOCUMENTOS-SHA256-15](../Tests/Documentos/Sha256Tests.cs), archivo válido distinto.
- [--] Validación de archivo modificado después de su carga original — [Implícito en caso TC-DOCUMENTOS-SHA256-15]
- [--] Comparación entre SHA256 registrado y SHA256 calculado — [Implícito en caso TC-DOCUMENTOS-SHA256-14 y TC-DOCUMENTOS-SHA256-15]
- [--] Visualización del hash almacenado en el sistema — [Implícito en caso TC-DOCUMENTOS-SHA256-05]
- [--] Visualización del hash calculado del archivo cargado — [Implícito en caso TC-DOCUMENTOS-SHA256-14 y TC-DOCUMENTOS-SHA256-15]
- [--] Validación de mensaje de integridad exitosa — [Implícito en caso TC-DOCUMENTOS-SHA256-14]
- [--] Validación de mensaje de integridad fallida — [Implícito en caso TC-DOCUMENTOS-SHA256-15]
- [x] Validación del botón "Validar" habilitado al seleccionar archivo — [TC-DOCUMENTOS-SHA256-01](../Tests/Documentos/Sha256Tests.cs) (Documento) y [TC-DOCUMENTOS-SHA256-16](../Tests/Documentos/Sha256Tests.cs) (Documento e Imagen).
- [--] Validación del botón "Validar" deshabilitado sin archivo seleccionado — [Implícito en caso TC-DOCUMENTOS-SHA256-11]
- [--] Consulta de documento por serial y validación de integridad — [Implícito en caso TC-DOCUMENTOS-SHA256-05 y TC-DOCUMENTOS-SHA256-14]
- [--] Consulta de imagen por serial y validación de integridad — [Implícito en caso TC-DOCUMENTOS-SHA256-05 y TC-DOCUMENTOS-SHA256-16]


