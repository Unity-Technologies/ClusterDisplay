using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Controllers
{
    [ApiController]
    [Route("api/v1/complexes")]
    public class ComplexesController : ControllerBase
    {
        public ComplexesController(ComplexesService complexesService,
                                   StatusService statusService)
        {
            m_ComplexesService = complexesService;
            m_StatusService = statusService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<LaunchComplex> retComplexes = new();
            using (var lockedAssets = await m_ComplexesService.Manager.GetLockedReadOnlyAsync())
            {
                foreach (var complex in lockedAssets.Value.Values)
                {
                    retComplexes.Add(complex.DeepClone());
                }
            }
            return Ok(retComplexes);
        }

        [Route("{Id}")]
        [HttpGet]
        public async Task<IActionResult> Get(Guid id)
        {
            LaunchComplex? retComplex;
            using (var lockedAssets = await m_ComplexesService.Manager.GetLockedReadOnlyAsync())
            {
                if (!lockedAssets.Value.TryGetValue(id, out retComplex))
                {
                    return NotFound($"Launch complex {id} not found");
                }
                retComplex = retComplex.DeepClone();
            }
            return Ok(retComplex);
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] LaunchComplex complex)
        {
            try
            {
                using (var lockedStatus = await m_StatusService.LockAsync())
                {
                    if (lockedStatus.Value.State != State.Idle)
                    {
                        return Conflict($"MissionControl has to be idle state to modify launch complexes (it is " +
                            $"currently {lockedStatus.Value.State}).");
                    }

                    m_ComplexesService.Manager.Put(complex);
                }

                await m_ComplexesService.SaveAsync();

                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.ToString());
            }
        }

        [Route("{Id}")]
        [HttpDelete]
        public async Task<IActionResult> Delete(Guid id)
        {
            using (var lockedStatus = await m_StatusService.LockAsync())
            {
                if (lockedStatus.Value.State != State.Idle)
                {
                    return Conflict($"MissionControl has to be idle state to modify launch complexes (it is " +
                        $"currently {lockedStatus.Value.State}).");
                }

                if (!m_ComplexesService.Manager.Remove(id))
                {
                    return NotFound($"No launch complex with the identifier of {id} can be found");
                }
            }

            await m_ComplexesService.SaveAsync();

            return Ok();
        }

        readonly ComplexesService m_ComplexesService;
        readonly StatusService m_StatusService;
    }
}
