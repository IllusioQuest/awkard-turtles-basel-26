# Type 2 job image (decision 0020): 2D Ising Monte Carlo research simulation, pure Python
# (standard library only, so the image builds and runs offline once the base image is present).
#
#   docker build -t digitalheat/research-sim jobs/research-sim
ARG PYTHON_IMAGE=python:3.12-slim
FROM ${PYTHON_IMAGE}
COPY ising.py /usr/local/bin/ising.py
# Parallelism comes from WORKERS processes (default: all CPUs the container may use).
ENV WORK=/work PYTHONUNBUFFERED=1
ENTRYPOINT ["python", "/usr/local/bin/ising.py"]
CMD ["run"]
