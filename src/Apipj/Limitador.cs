using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Apipj
{
    /// <summary>
    /// Limitador de vazão do ConsultarVariosAsync: janela deslizante de <c>limite</c> inícios de requisição
    /// por minuto, com o limite lido de <c>X-RateLimit-Limit</c>. A primeira consulta sai sozinha (sonda)
    /// para descobrir o limite do plano antes de abrir as demais.
    /// </summary>
    internal sealed class Limitador
    {
        private readonly object _trava = new object();
        private readonly Queue<DateTime> _inicios = new Queue<DateTime>();
        private readonly TaskCompletionSource<bool> _sonda = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TimeSpan _janela;
        private readonly Func<DateTime> _agora;
        private int? _limite;
        private bool _sondaTomada;
        private DateTime _bloqueadoAte = DateTime.MinValue;

        public Limitador(TimeSpan janela, Func<DateTime>? agora = null)
        {
            _janela = janela;
            _agora = agora ?? (() => DateTime.UtcNow);
        }

        /// <summary>Limite efetivo por janela (null até a primeira resposta com cabeçalho).</summary>
        public int? Limite { get { lock (_trava) return _limite; } }

        /// <summary>
        /// Porta de entrada de cada consulta do lote: a primeira chamada vira a sonda e passa na hora (devolve true);
        /// as demais esperam a sonda terminar (<see cref="LiberarSonda"/>).
        /// </summary>
        public async Task<bool> EntrarAsync(CancellationToken ct)
        {
            lock (_trava)
            {
                if (!_sondaTomada)
                {
                    _sondaTomada = true;
                    return true;
                }
            }
            if (!_sonda.Task.IsCompleted)
            {
                var cancelada = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (ct.Register(() => cancelada.TrySetCanceled(ct)))
                {
                    await Task.WhenAny(_sonda.Task, cancelada.Task).ConfigureAwait(false);
                }
            }
            ct.ThrowIfCancellationRequested();
            return false;
        }

        /// <summary>Acorda quem espera a sonda (idempotente).</summary>
        public void LiberarSonda() => _sonda.TrySetResult(true);

        /// <summary>Espera até uma nova requisição poder começar e já a registra na janela.</summary>
        public async Task AguardarJanelaAsync(Func<TimeSpan, CancellationToken, Task> esperar, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                TimeSpan espera;
                lock (_trava)
                {
                    var agora = _agora();
                    while (_inicios.Count > 0 && _inicios.Peek() <= agora - _janela) _inicios.Dequeue();
                    espera = _bloqueadoAte > agora ? _bloqueadoAte - agora : TimeSpan.Zero;
                    if (_limite is int limite && limite > 0 && _inicios.Count >= limite)
                    {
                        var arr = _inicios.ToArray();
                        var liberaEm = arr[arr.Length - limite] + _janela - agora;
                        if (liberaEm > espera) espera = liberaEm;
                    }
                    if (espera <= TimeSpan.Zero)
                    {
                        _inicios.Enqueue(agora);
                        return;
                    }
                }
                await esperar(espera, ct).ConfigureAwait(false);
            }
        }

        /// <summary>Registra os cabeçalhos de limite de uma resposta.</summary>
        public void Registrar(long? limite, long? restante)
        {
            lock (_trava)
            {
                if (limite is long l && l > 0)
                {
                    _limite = (int)Math.Min(int.MaxValue, l);
                    if (restante == 0) BloquearSemTrava(TimeSpan.FromTicks(_janela.Ticks / l));
                }
            }
        }

        /// <summary>Ninguém começa nada pelo tempo informado (Retry-After, Remaining = 0).</summary>
        public void Bloquear(TimeSpan tempo)
        {
            lock (_trava) BloquearSemTrava(tempo);
        }

        private void BloquearSemTrava(TimeSpan tempo)
        {
            var ate = _agora() + tempo;
            if (ate > _bloqueadoAte) _bloqueadoAte = ate;
        }
    }
}
