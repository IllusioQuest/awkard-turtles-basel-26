# Type 2 job image (decision 0020): 2D Ising Monte Carlo research simulation, Python + NumPy.
# Runs offline once built.
#
#   docker build -t digitalheat/research-sim jobs/research-sim
ARG PYTHON_IMAGE=python:3.12-slim
FROM ${PYTHON_IMAGE}
ARG NUMPY_VERSION=2.2.6
RUN pip install --no-cache-dir "numpy==${NUMPY_VERSION}"
COPY ising.py /usr/local/bin/ising.py
LABEL org.digitalheat.numpy-version="${NUMPY_VERSION}"
# One NumPy thread per worker process; parallelism comes from WORKERS processes.
ENV WORK=/work OMP_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 PYTHONUNBUFFERED=1
ENTRYPOINT ["python", "/usr/local/bin/ising.py"]
CMD ["run"]
