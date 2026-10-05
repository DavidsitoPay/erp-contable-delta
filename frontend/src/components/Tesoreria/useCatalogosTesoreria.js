import { useCallback, useEffect, useState } from "react";
import { cuentasApi, cuentasBancariasApi } from "../../services/api";

export function useCatalogosTesoreria(vista) {
  const [cuentasBancarias, setCuentasBancarias] = useState([]);
  const [cuentasContables, setCuentasContables] = useState([]);
  const [error, setError] = useState("");

  const recargarCuentasBancarias = useCallback(async () => {
    try {
      const { data } = await cuentasBancariasApi.listar(true);
      setCuentasBancarias(data);
    } catch {
      setError("No se pudieron cargar las cuentas bancarias.");
    }
  }, []);

  useEffect(() => {
    void recargarCuentasBancarias();
  }, [recargarCuentasBancarias, vista]);

  useEffect(() => {
    cuentasApi.listar(true).then(({ data }) => setCuentasContables(data)).catch(() => {
      setError("No se pudieron cargar las cuentas contables.");
    });
  }, []);

  return { cuentasBancarias, cuentasContables, recargarCuentasBancarias, error };
}
