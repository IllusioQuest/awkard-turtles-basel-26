ARG PYTHON_IMAGE=python:3.12-alpine@sha256:c5ba3deef00c7758561a0f03c26f44f10589263d40e732b8748e0d1f38f6dd89
FROM ${PYTHON_IMAGE}
COPY ising.py /opt/mesh/ising.py
COPY run.sh /opt/mesh/run.sh
RUN sed -i 's/\r$//' /opt/mesh/run.sh && chmod +x /opt/mesh/run.sh && mkdir /work && chown 65534:65534 /work
USER 65534:65534
# Each job gets its own Docker-managed persistent volume; never share one job's results.
VOLUME ["/work"]
ENTRYPOINT ["/opt/mesh/run.sh"]
