We are a team of three working at the Basel GreenTech hackathon.
Our task is developing a solution for the company DigitalHeat. They sell server racks to households who simultaneously heat the house.
Assume one server produces 1kW heat from 1kW electricity. This heat is transferred to the home with 80% efficiency, so 1kW electricity generates 0.8kW heat.
The average house in Switzerland heats 206 days / year. Demand per year for rooms: 27000kWh; Demand per year for hot water: 4000kWh. 31000kWh / year in total. 
Assume every household has a water tank of 35kWh capacity with no loss. 
Electricity in Basel for a H4 household costs about 0.3325CHF/kWH. Assume the average heat pump is 2.5 times more efficient than our servers.
Thus the value a server delivers is electricity price / 2.5. We are developing a system that sells compute on these servers. 
The goal is to find compute that brings more value than (electricity price - value server provides) * fraction of kWh actually sold. Heat demand varies strongly between days and seasons of the year.
The system must intelligently schedule compute jobs across the distributed server network depending on every households current demand.
It can also plan ahend load balancing using heat demand and weather estimates. The type of compute sold must provide enough value but should be easy to pause, parallelize and reschedule.
A competitive advantage is that all code is executed on Swiss servers. Our current main ideas are Apertus 8B inference and running whisper large on GPUs as main jobs. 
The excess compute can be used for research simulations like molecule docking or monte carlo simulations. The pricing for latter would depend on the current heat demand and overall network load.
Current open tasks:
- Define job types, their deadline, interruptability and splittability. For every Job measure the runtime on a local device, scales the exact energy consumption values to a server and maybe also tests running on a Google Colab GPU.
- Predict job demand (who will buy our compute?) and cite sources
- Schedule, distribute, pause, reschedule tasks and create local demos.
- Make a large scale simulation of a canton or Switzerland using real weather data and historic household heat demand. Simulate compute demand and run the scheduler. Make realistic assumptions for everything else. Many parameters should be tunable. After every simulation calculate all metrics per house, heat demand, heat dumped, utilization, missed deadlines, electricity cost, computing income and break-even.
- Optimize the number of servers per household. (RL? Evolutionionary Algorithms? Informed Guessing?)
- If there's time: Incorporate possible solar panels or better heat storage.

Open questions:
- Should we take into account hardware costs and lifetime in our profitability calculation?
